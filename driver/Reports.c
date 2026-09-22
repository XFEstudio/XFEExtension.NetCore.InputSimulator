#include "Protocol.h"

void XfeClearState(XFE_STATE* state)
{
    uint32_t i;
    state->Keyboard.Id = 1;
    state->Keyboard.Modifiers = 0;
    state->Keyboard.Reserved = 0;
    for (i = 0; i < XFE_MAX_KEYS; ++i) state->Keyboard.Keys[i] = 0;
    state->Buttons = 0;
}

enum XFE_RESULT XfeEncode(const XFE_STATE* current, const XFE_COMMAND* command,
    XFE_STATE* next, XFE_REPORT* report, uint32_t* reportSize)
{
    uint32_t i, empty = XFE_MAX_KEYS;
    uint8_t mask;
    *reportSize = 0;
    if (command->Version != XFE_PROTOCOL_VERSION || command->Reserved != 0)
        return XfeInvalidCommand;
    *next = *current;
    if (command->Kind == XFE_KEY) {
        if (command->Flags > 1 || command->X || command->Y || command->Delta ||
            !((command->Code >= 4 && command->Code <= 0x73) ||
              (command->Code >= 0xE0 && command->Code <= 0xE7))) return XfeInvalidCommand;
        if (command->Code >= 0xE0) {
            mask = (uint8_t)(1u << (command->Code - 0xE0));
            if (command->Flags) next->Keyboard.Modifiers &= (uint8_t)~mask;
            else next->Keyboard.Modifiers |= mask;
        } else {
            for (i = 0; i < XFE_MAX_KEYS; ++i) {
                if (next->Keyboard.Keys[i] == command->Code) break;
                if (next->Keyboard.Keys[i] == 0) empty = i;
            }
            if (command->Flags) {
                if (i < XFE_MAX_KEYS) next->Keyboard.Keys[i] = 0;
            } else if (i == XFE_MAX_KEYS) {
                if (empty == XFE_MAX_KEYS) return XfeTooManyKeys;
                next->Keyboard.Keys[empty] = (uint8_t)command->Code;
            }
        }
        report->Keyboard = next->Keyboard;
        *reportSize = sizeof(XFE_KEYBOARD_REPORT);
        return XfeSuccess;
    }

    report->Mouse.Id = 2;
    report->Mouse.Buttons = current->Buttons;
    report->Mouse.X = report->Mouse.Y = 0;
    report->Mouse.Wheel = report->Mouse.Pan = 0;
    switch (command->Kind) {
    case XFE_BUTTON:
        if (command->Code > 4 || command->Flags > 1 || command->X || command->Y || command->Delta)
            return XfeInvalidCommand;
        mask = (uint8_t)(1u << command->Code);
        if (command->Flags) next->Buttons &= (uint8_t)~mask;
        else next->Buttons |= mask;
        report->Mouse.Buttons = next->Buttons;
        break;
    case XFE_MOVE:
        if (command->Code || command->Flags || command->Delta || command->X < -32767 ||
            command->X > 32767 || command->Y < -32767 || command->Y > 32767)
            return XfeInvalidCommand;
        report->Mouse.X = (int16_t)command->X;
        report->Mouse.Y = (int16_t)command->Y;
        break;
    case XFE_WHEEL:
        if (command->Code || command->Flags > 1 || command->X || command->Y ||
            command->Delta < -127 || command->Delta > 127) return XfeInvalidCommand;
        if (command->Flags) report->Mouse.Pan = (int8_t)command->Delta;
        else report->Mouse.Wheel = (int8_t)command->Delta;
        break;
    default:
        return XfeInvalidCommand;
    }
    *reportSize = sizeof(XFE_MOUSE_REPORT);
    return XfeSuccess;
}
