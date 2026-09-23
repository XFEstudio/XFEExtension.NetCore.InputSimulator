#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include "Protocol.h"

#define CHECK(x) do { if (!(x)) { fprintf(stderr, "Failed at line %d: %s\n", __LINE__, #x); exit(1); } } while (0)

int main(void)
{
    XFE_STATE state, next;
    XFE_COMMAND cmd = { XFE_PROTOCOL_VERSION, XFE_KEY, 4, 0, 0, 0, 0, 0 };
    XFE_REPORT report;
    uint32_t size, i;
    XfeClearState(&state);
    CHECK(sizeof(cmd) == 32 && sizeof(XFE_INFO) == 16);
    CHECK(sizeof(XFE_KEYBOARD_REPORT) == 9 && sizeof(XFE_MOUSE_REPORT) == 8);
    for (i = 0; i < 6; ++i) {
        cmd.Code = 4 + i;
        CHECK(XfeEncode(&state, &cmd, &next, &report, &size) == XfeSuccess);
        CHECK(size == 9 && report.Keyboard.Id == 1);
        state = next;
    }
    cmd.Code = 10;
    CHECK(XfeEncode(&state, &cmd, &next, &report, &size) == XfeTooManyKeys);
    cmd.Code = 0xE5;
    CHECK(XfeEncode(&state, &cmd, &next, &report, &size) == XfeSuccess);
    CHECK(next.Keyboard.Modifiers == 0x20);
    state = next;
    cmd.Flags = 1;
    CHECK(XfeEncode(&state, &cmd, &next, &report, &size) == XfeSuccess);
    CHECK(next.Keyboard.Modifiers == 0);
    state = next;
    cmd.Code = 5;
    CHECK(XfeEncode(&state, &cmd, &next, &report, &size) == XfeSuccess);
    state = next;
    cmd.Code = 10; cmd.Flags = 0;
    CHECK(XfeEncode(&state, &cmd, &next, &report, &size) == XfeSuccess);
    state = next;
    CHECK(XfeEncode(&state, &cmd, &next, &report, &size) == XfeSuccess); /* duplicate down */
    CHECK(memcmp(&state, &next, sizeof(state)) == 0);
    cmd.Code = 0;
    CHECK(XfeEncode(&state, &cmd, &next, &report, &size) == XfeInvalidCommand);
    cmd.Code = 4; cmd.Reserved = 1;
    CHECK(XfeEncode(&state, &cmd, &next, &report, &size) == XfeInvalidCommand);
    cmd.Reserved = 0; cmd.Version++;
    CHECK(XfeEncode(&state, &cmd, &next, &report, &size) == XfeInvalidCommand);
    cmd.Version = XFE_PROTOCOL_VERSION; cmd.Kind = XFE_BUTTON;
    for (i = 0; i < 5; ++i) {
        cmd.Code = i;
        CHECK(XfeEncode(&state, &cmd, &next, &report, &size) == XfeSuccess);
        CHECK(size == 8 && report.Mouse.Id == 2);
        state = next;
    }
    CHECK(state.Buttons == 31);
    cmd.Kind = XFE_MOVE; cmd.Code = 0; cmd.X = -32767; cmd.Y = 32767;
    CHECK(XfeEncode(&state, &cmd, &next, &report, &size) == XfeSuccess);
    CHECK(report.Mouse.X == -32767 && report.Mouse.Y == 32767 && report.Mouse.Buttons == 31);
    cmd.X = -32768;
    CHECK(XfeEncode(&state, &cmd, &next, &report, &size) == XfeInvalidCommand);
    cmd.Kind = XFE_WHEEL; cmd.X = cmd.Y = 0; cmd.Delta = -127;
    CHECK(XfeEncode(&state, &cmd, &next, &report, &size) == XfeSuccess);
    CHECK(report.Mouse.Wheel == -127 && report.Mouse.Pan == 0);
    cmd.Flags = 1; cmd.Delta = 127;
    CHECK(XfeEncode(&state, &cmd, &next, &report, &size) == XfeSuccess);
    CHECK(report.Mouse.Wheel == 0 && report.Mouse.Pan == 127);
    cmd.Delta = 128;
    CHECK(XfeEncode(&state, &cmd, &next, &report, &size) == XfeInvalidCommand);
    XfeClearState(&state);
    CHECK(state.Keyboard.Id == 1 && state.Keyboard.Modifiers == 0 && state.Buttons == 0);
    for (i = 0; i < 6; ++i) CHECK(state.Keyboard.Keys[i] == 0);
    puts("Native report tests passed: ABI, rollover, modifiers, release, buttons, movement, wheels, validation.");
    return 0;
}
