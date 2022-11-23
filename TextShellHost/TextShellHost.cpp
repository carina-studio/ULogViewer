#include <sys/ioctl.h>
#include <stdint.h>
#include <stdio.h>


extern "C" int32_t TextShellHost_GetOutputWindowSize(uint16_t* columns, uint16_t* rows)
{
    winsize size;
    int result = ioctl(1, TIOCGWINSZ, &size);
    if (result == 0)
    {
        *columns = size.ws_col;
        *rows = size.ws_row;
        return 0;
    }
    *columns = 0;
    *rows = 0;
    return result;
}


extern "C" int32_t TextShellHost_SetOutputWindowSize(uint16_t columns, uint16_t rows)
{
    winsize size = { rows, columns, 0, 0 };
    return ioctl(1, TIOCSWINSZ, &size);
}