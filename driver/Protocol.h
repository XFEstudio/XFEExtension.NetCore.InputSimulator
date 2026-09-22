#pragma once
#include <stdint.h>

/* Shared, pointer-free ABI. Keep Native/DriverProtocol.cs in sync. */
#define XFE_PROTOCOL_VERSION 1u
#define XFE_CAPABILITIES 31u
#define XFE_MAX_KEYS 6u
#define XFE_KEY 1u
#define XFE_BUTTON 2u
#define XFE_MOVE 3u
#define XFE_WHEEL 4u

typedef struct XFE_COMMAND {
    uint32_t Version, Kind, Code, Flags;
    int32_t X, Y, Delta;
    uint32_t Reserved;
} XFE_COMMAND;

typedef struct XFE_INFO {
    uint32_t Version, Capabilities, MaxKeys, Reserved;
} XFE_INFO;

#pragma pack(push, 1)
typedef struct XFE_KEYBOARD_REPORT {
    uint8_t Id, Modifiers, Reserved, Keys[XFE_MAX_KEYS];
} XFE_KEYBOARD_REPORT;

typedef struct XFE_MOUSE_REPORT {
    uint8_t Id, Buttons;
    int16_t X, Y;
    int8_t Wheel, Pan;
} XFE_MOUSE_REPORT;
#pragma pack(pop)

typedef struct XFE_STATE {
    XFE_KEYBOARD_REPORT Keyboard;
    uint8_t Buttons;
} XFE_STATE;

typedef union XFE_REPORT {
    XFE_KEYBOARD_REPORT Keyboard;
    XFE_MOUSE_REPORT Mouse;
} XFE_REPORT;

enum XFE_RESULT { XfeSuccess, XfeInvalidCommand, XfeTooManyKeys };

void XfeClearState(XFE_STATE* state);
/* Produces a candidate state. The caller commits it only after enqueueing the report. */
enum XFE_RESULT XfeEncode(const XFE_STATE* current, const XFE_COMMAND* command,
    XFE_STATE* next, XFE_REPORT* report, uint32_t* reportSize);
