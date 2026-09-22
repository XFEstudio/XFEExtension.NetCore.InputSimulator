#define UNICODE
#define _UNICODE
#include <windows.h>
#include <setupapi.h>
#include <newdev.h>
#include <initguid.h>
#include <devguid.h>
#include <stdio.h>
#include <wincrypt.h>
#include <wintrust.h>
#include <softpub.h>

/* Elevated helper embedded in the managed DLL. Uses only supported PnP APIs. */
static DWORD Install(const wchar_t* inf)
{
    HDEVINFO devices;
    SP_DEVINFO_DATA device;
    DWORD index, error = ERROR_SUCCESS;
    BOOL found = FALSE, created = FALSE, reboot = FALSE, stageReboot = FALSE;
    wchar_t ids[1024];
    const wchar_t hardwareId[] = L"Root\\XFEInput\0";
    if (!DiInstallDriverW(NULL, inf, 0, &stageReboot)) return GetLastError();
    devices = SetupDiGetClassDevsW(&GUID_DEVCLASS_HIDCLASS, NULL, NULL, DIGCF_PRESENT);
    if (devices == INVALID_HANDLE_VALUE) return GetLastError();
    ZeroMemory(&device, sizeof(device));
    device.cbSize = sizeof(device);
    for (index = 0; SetupDiEnumDeviceInfo(devices, index, &device); ++index) {
        if (SetupDiGetDeviceRegistryPropertyW(devices, &device, SPDRP_HARDWAREID,
            NULL, (BYTE*)ids, sizeof(ids), NULL)) {
            const wchar_t* id;
            ids[ARRAYSIZE(ids) - 1] = 0;
            for (id = ids; *id; id += wcslen(id) + 1)
                if (_wcsicmp(id, L"Root\\XFEInput") == 0) { found = TRUE; break; }
            if (found) break;
        }
    }
    if (!found) {
        error = GetLastError();
        if (error != ERROR_NO_MORE_ITEMS) goto done;
        if (!SetupDiCreateDeviceInfoW(devices, L"XFEInput", &GUID_DEVCLASS_HIDCLASS,
            L"XFE UMDF Virtual Keyboard and Mouse", NULL, DICD_GENERATE_ID, &device) ||
            !SetupDiSetDeviceRegistryPropertyW(devices, &device, SPDRP_HARDWAREID,
                (const BYTE*)hardwareId, sizeof(hardwareId)) ||
            !SetupDiCallClassInstaller(DIF_REGISTERDEVICE, devices, &device)) {
            error = GetLastError();
            goto done;
        }
        created = TRUE;
    }
    if (!UpdateDriverForPlugAndPlayDevicesW(NULL, L"Root\\XFEInput", inf, INSTALLFLAG_FORCE, &reboot)) {
        error = GetLastError();
        if (created) (void)SetupDiCallClassInstaller(DIF_REMOVE, devices, &device);
        goto done;
    }
    error = (reboot || stageReboot) ? ERROR_SUCCESS_REBOOT_REQUIRED : ERROR_SUCCESS;
done:
    SetupDiDestroyDeviceInfoList(devices);
    return error;
}

static LONG VerifyFile(const wchar_t* path, PCCERT_CONTEXT certificate)
{
    GUID policy = WINTRUST_ACTION_GENERIC_VERIFY_V2;
    WINTRUST_FILE_INFO file = { 0 };
    WINTRUST_DATA data = { 0 };
    CRYPT_PROVIDER_DATA* provider;
    CRYPT_PROVIDER_SGNR* signer;
    LONG result;
    file.cbStruct = sizeof(file);
    file.pcwszFilePath = path;
    data.cbStruct = sizeof(data);
    data.dwUIChoice = WTD_UI_NONE;
    data.fdwRevocationChecks = WTD_REVOKE_NONE;
    data.dwUnionChoice = WTD_CHOICE_FILE;
    data.pFile = &file;
    data.dwStateAction = WTD_STATEACTION_VERIFY;
    data.dwProvFlags = WTD_CACHE_ONLY_URL_RETRIEVAL;
    result = WinVerifyTrust(NULL, &policy, &data);
    provider = WTHelperProvDataFromStateData(data.hWVTStateData);
    signer = provider ? WTHelperGetProvSignerFromChain(provider, 0, FALSE, 0) : NULL;
    if (!signer || !signer->csCertChain || !signer->pasCertChain[0].pCert ||
        signer->pasCertChain[0].pCert->cbCertEncoded != certificate->cbCertEncoded ||
        memcmp(signer->pasCertChain[0].pCert->pbCertEncoded, certificate->pbCertEncoded, certificate->cbCertEncoded))
        result = TRUST_E_SUBJECT_NOT_TRUSTED;
    data.dwStateAction = WTD_STATEACTION_CLOSE;
    WinVerifyTrust(NULL, &policy, &data);
    return result;
}

static BOOL Contains(HCERTSTORE store, PCCERT_CONTEXT certificate)
{
    PCCERT_CONTEXT found = CertFindCertificateInStore(store, X509_ASN_ENCODING, 0, CERT_FIND_EXISTING, certificate, NULL);
    if (!found) return FALSE;
    CertFreeCertificateContext(found);
    return TRUE;
}

static void UndoTrust(HCERTSTORE store, PCCERT_CONTEXT certificate, BOOL added)
{
    PCCERT_CONTEXT found;
    if (!added) return;
    found = CertFindCertificateInStore(store, X509_ASN_ENCODING, 0, CERT_FIND_EXISTING, certificate, NULL);
    if (found) CertDeleteCertificateFromStore(found);
}

static DWORD InstallSigned(const wchar_t* inf, const wchar_t* certificatePath, const wchar_t* expectedHash)
{
    PCCERT_CONTEXT certificate = NULL;
    HCERTSTORE root = NULL, publisher = NULL;
    CERT_ENHKEY_USAGE* usage = NULL;
    CERT_BASIC_CONSTRAINTS2_INFO* constraints = NULL;
    PCERT_EXTENSION extension;
    DWORD size, length, error = ERROR_INVALID_DATA;
    BYTE hash[32];
    wchar_t actualHash[65], subject[256], question[1200];
    wchar_t folder[32768], dll[32768], catalog[32768], executable[32768];
    wchar_t* slash;
    BOOL addedRoot = FALSE, addedPublisher = FALSE, needsRoot;
    LONG dllStatus, catalogStatus, helperStatus;
    unsigned i;
    if (wcslen(expectedHash) != 64 || !CryptQueryObject(CERT_QUERY_OBJECT_FILE, certificatePath,
        CERT_QUERY_CONTENT_FLAG_CERT, CERT_QUERY_FORMAT_FLAG_BINARY, 0, NULL, NULL, NULL, NULL, NULL,
        (const void**)&certificate)) return ERROR_INVALID_DATA;
    size = sizeof(hash);
    if (!CryptHashCertificate2(L"SHA256", 0, NULL, certificate->pbCertEncoded, certificate->cbCertEncoded, hash, &size)) goto done;
    for (i = 0; i < sizeof(hash); ++i) swprintf_s(actualHash + i * 2, 3, L"%02X", hash[i]);
    if (_wcsicmp(actualHash, expectedHash) || CertVerifyTimeValidity(NULL, certificate->pCertInfo)) goto done;
    size = 0;
    if (!CertGetEnhancedKeyUsage(certificate, 0, NULL, &size)) goto done;
    usage = (CERT_ENHKEY_USAGE*)LocalAlloc(LPTR, size);
    if (!usage) { error = ERROR_OUTOFMEMORY; goto done; }
    if (!CertGetEnhancedKeyUsage(certificate, 0, usage, &size) || usage->cUsageIdentifier != 1 ||
        strcmp(usage->rgpszUsageIdentifier[0], "1.3.6.1.5.5.7.3.3")) goto done;
    extension = CertFindExtension(szOID_BASIC_CONSTRAINTS2, certificate->pCertInfo->cExtension, certificate->pCertInfo->rgExtension);
    if (!extension || !CryptDecodeObjectEx(X509_ASN_ENCODING, X509_BASIC_CONSTRAINTS2,
        extension->Value.pbData, extension->Value.cbData, CRYPT_DECODE_ALLOC_FLAG, NULL, &constraints, &size) ||
        constraints->fCA) goto done;
    length = GetFullPathNameW(inf, ARRAYSIZE(folder), folder, NULL);
    if (!length || length >= ARRAYSIZE(folder)) goto done;
    slash = wcsrchr(folder, L'\\');
    if (!slash || _wcsicmp(slash + 1, L"XfeInput.inf")) goto done;
    *slash = 0;
    if (swprintf_s(dll, ARRAYSIZE(dll), L"%ls\\XfeInputDriver.dll", folder) < 0 ||
        swprintf_s(catalog, ARRAYSIZE(catalog), L"%ls\\XfeInput.cat", folder) < 0 ||
        !GetModuleFileNameW(NULL, executable, ARRAYSIZE(executable))) goto done;
    dllStatus = VerifyFile(dll, certificate);
    catalogStatus = VerifyFile(catalog, certificate);
    helperStatus = VerifyFile(executable, certificate);
    if ((dllStatus && dllStatus != CERT_E_UNTRUSTEDROOT) ||
        (catalogStatus && catalogStatus != CERT_E_UNTRUSTEDROOT) ||
        (helperStatus && helperStatus != CERT_E_UNTRUSTEDROOT)) { error = ERROR_INVALID_IMAGE_HASH; goto done; }
    needsRoot = dllStatus != ERROR_SUCCESS || catalogStatus != ERROR_SUCCESS || helperStatus != ERROR_SUCCESS;
    if (needsRoot && !CertCompareCertificateName(X509_ASN_ENCODING,
        &certificate->pCertInfo->Subject, &certificate->pCertInfo->Issuer)) goto done;
    root = CertOpenStore(CERT_STORE_PROV_SYSTEM_W, 0, 0, CERT_SYSTEM_STORE_LOCAL_MACHINE, L"Root");
    publisher = CertOpenStore(CERT_STORE_PROV_SYSTEM_W, 0, 0, CERT_SYSTEM_STORE_LOCAL_MACHINE, L"TrustedPublisher");
    if (!root || !publisher) { error = GetLastError(); goto done; }
    if ((needsRoot && !Contains(root, certificate)) || !Contains(publisher, certificate)) {
        CertGetNameStringW(certificate, CERT_NAME_SIMPLE_DISPLAY_TYPE, 0, NULL, subject, ARRAYSIZE(subject));
        swprintf_s(question, ARRAYSIZE(question),
            L"安装 XFE 用户态键鼠驱动需要信任以下代码签名者：\n\n%ls\n\n证书 SHA-256：\n%ls\n\n"
            L"将添加到本机 TrustedPublisher；自签名证书还会添加到 Root。系统会信任此证书签名的代码。"
            L"这不是微软 WHQL 签名，不会更改 Secure Boot 或开启测试模式。\n\n是否信任并安装？", subject, actualHash);
        if (MessageBoxW(NULL, question, L"XFE 驱动：确认签名者信任", MB_YESNO | MB_ICONWARNING | MB_DEFBUTTON2 | MB_SETFOREGROUND) != IDYES) {
            error = ERROR_CANCELLED; goto done;
        }
        if (needsRoot && !Contains(root, certificate)) {
            if (!CertAddCertificateContextToStore(root, certificate, CERT_STORE_ADD_NEW, NULL)) { error = GetLastError(); goto done; }
            addedRoot = TRUE;
        }
        if (!Contains(publisher, certificate)) {
            if (!CertAddCertificateContextToStore(publisher, certificate, CERT_STORE_ADD_NEW, NULL)) { error = GetLastError(); goto done; }
            addedPublisher = TRUE;
        }
    }
    if (VerifyFile(dll, certificate) || VerifyFile(catalog, certificate) || VerifyFile(executable, certificate)) {
        error = ERROR_INVALID_IMAGE_HASH; goto done;
    }
    error = Install(inf);
done:
    if (error != ERROR_SUCCESS && error != ERROR_SUCCESS_REBOOT_REQUIRED) {
        if (root) UndoTrust(root, certificate, addedRoot);
        if (publisher) UndoTrust(publisher, certificate, addedPublisher);
    }
    if (root) CertCloseStore(root, 0);
    if (publisher) CertCloseStore(publisher, 0);
    if (certificate) CertFreeCertificateContext(certificate);
    if (usage) LocalFree(usage);
    if (constraints) LocalFree(constraints);
    return error;
}

int wmain(int argc, wchar_t** argv)
{
    DWORD error;
    DWORD wait;
    HANDLE mutex;
    if (argc != 5 || wcscmp(argv[1], L"install") != 0) {
        fwprintf(stderr, L"Usage: XfeInputSetup.exe install <full-inf-path> <public-certificate> <certificate-SHA256>\n");
        return ERROR_BAD_ARGUMENTS;
    }
    mutex = CreateMutexW(NULL, FALSE, L"Global\\XFEInput.DriverSetup.v1");
    if (!mutex) return (int)GetLastError();
    wait = WaitForSingleObject(mutex, 120000);
    if (wait != WAIT_OBJECT_0 && wait != WAIT_ABANDONED) {
        error = wait == WAIT_TIMEOUT ? ERROR_TIMEOUT : GetLastError();
        CloseHandle(mutex);
        return (int)error;
    }
    error = InstallSigned(argv[2], argv[3], argv[4]);
    ReleaseMutex(mutex);
    CloseHandle(mutex);
    if (error) fwprintf(stderr, L"Driver installation returned Win32 error %lu.\n", error);
    return (int)error;
}
