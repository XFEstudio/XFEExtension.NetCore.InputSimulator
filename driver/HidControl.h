#pragma once
#include "Protocol.h"

#define XFE_CONTROL_REPORT_ID 3u
#define XFE_CONTROL_REPORT_SIZE 64u
#define XFE_CONTROL_MAGIC 0x55454658u /* XFEU */
#define XFE_LEASE_TIMEOUT_MS 3000u
#define XFE_QUEUE_CAPACITY 64u
#define XFE_ACQUIRE 1u
#define XFE_SEND 2u
#define XFE_RESET 3u
#define XFE_HEARTBEAT 4u
#define XFE_RELEASE 5u

#pragma pack(push, 1)
typedef struct XFE_CONTROL_PACKET {
    uint8_t Id, Operation;
    uint16_t Reserved0;
    uint32_t Version;
    uint64_t Session;
    XFE_COMMAND Command;
    uint32_t Reserved[4];
} XFE_CONTROL_PACKET;
typedef struct XFE_CONTROL_INFO {
    uint8_t Id, Reserved0[3];
    uint32_t Magic;
    XFE_INFO Protocol;
    uint32_t LeaseTimeoutMs, QueueCapacity, QueuedReports, LeaseActive;
    uint8_t Reserved[24];
} XFE_CONTROL_INFO;
#pragma pack(pop)

typedef struct XFE_PENDING_REPORT { XFE_REPORT Report; uint32_t Length; } XFE_PENDING_REPORT;
typedef struct XFE_CONTROL_STATE {
    XFE_STATE Input;
    XFE_PENDING_REPORT Queue[XFE_QUEUE_CAPACITY];
    uint32_t Head, Count;
    uint64_t Session, LastHeartbeat;
} XFE_CONTROL_STATE;
enum XFE_CONTROL_RESULT { XfeControlSuccess, XfeControlInvalid, XfeControlBusy, XfeControlNotOwner };
void XfeControlInitialize(XFE_CONTROL_STATE* state);
void XfeControlExpire(XFE_CONTROL_STATE* state, uint64_t now);
void XfeControlGetInfo(const XFE_CONTROL_STATE* state, XFE_CONTROL_INFO* info);
enum XFE_CONTROL_RESULT XfeControlApply(XFE_CONTROL_STATE* state, const XFE_CONTROL_PACKET* packet, uint64_t now);
const XFE_PENDING_REPORT* XfeControlPeek(const XFE_CONTROL_STATE* state);
void XfeControlPop(XFE_CONTROL_STATE* state);
