#include <windows.h>
#include <shellapi.h>
#include <objbase.h>

#include <string>
#include <string_view>
#include <vector>

namespace
{
constexpr DWORD ExitSuccess = 0;
constexpr DWORD ExitCleanupFailed = 1;
constexpr DWORD ExitInvalidOrElevationFailed = 2;

struct unique_handle
{
    HANDLE value = nullptr;

    unique_handle() = default;
    explicit unique_handle(HANDLE handle) noexcept : value(handle) {}
    unique_handle(const unique_handle&) = delete;
    unique_handle& operator=(const unique_handle&) = delete;

    ~unique_handle()
    {
        if (value != nullptr && value != INVALID_HANDLE_VALUE)
        {
            CloseHandle(value);
        }
    }

    [[nodiscard]] HANDLE get() const noexcept
    {
        return value;
    }
};

[[nodiscard]] bool equals_ignore_case(std::wstring_view left, std::wstring_view right) noexcept
{
    return left.size() == right.size()
        && CompareStringOrdinal(
            left.data(),
            static_cast<int>(left.size()),
            right.data(),
            static_cast<int>(right.size()),
            TRUE) == CSTR_EQUAL;
}

[[nodiscard]] bool is_clean_argument(std::wstring_view argument) noexcept
{
    return equals_ignore_case(argument, L"-clean")
        || equals_ignore_case(argument, L"/clean")
        || equals_ignore_case(argument, L"-clean:full")
        || equals_ignore_case(argument, L"/clean:full");
}

[[nodiscard]] bool is_help_argument(std::wstring_view argument) noexcept
{
    return equals_ignore_case(argument, L"-h")
        || equals_ignore_case(argument, L"--help")
        || equals_ignore_case(argument, L"/?");
}

void write_utf8(HANDLE stream, std::string_view text)
{
    DWORD consoleMode = 0;
    if (GetConsoleMode(stream, &consoleMode) != FALSE)
    {
        const int characterCount = MultiByteToWideChar(
            CP_UTF8,
            MB_ERR_INVALID_CHARS,
            text.data(),
            static_cast<int>(text.size()),
            nullptr,
            0);
        if (characterCount <= 0)
        {
            return;
        }

        std::wstring wideText(static_cast<size_t>(characterCount), L'\0');
        if (MultiByteToWideChar(
                CP_UTF8,
                MB_ERR_INVALID_CHARS,
                text.data(),
                static_cast<int>(text.size()),
                wideText.data(),
                characterCount) <= 0)
        {
            return;
        }

        DWORD written = 0;
        WriteConsoleW(
            stream,
            wideText.data(),
            static_cast<DWORD>(wideText.size()),
            &written,
            nullptr);
        return;
    }

    DWORD written = 0;
    WriteFile(
        stream,
        text.data(),
        static_cast<DWORD>(text.size()),
        &written,
        nullptr);
}

void write_line(HANDLE stream, std::string_view text)
{
    write_utf8(stream, text);
    if (text.empty() || text.back() != '\n')
    {
        write_utf8(stream, "\r\n");
    }
}

void write_fallback_help(HANDLE stream)
{
    write_line(
        stream,
        "Mem Reduct WinUI command-line tool\r\n"
        "\r\n"
        "  mrw-cli\r\n"
        "      Show this help information.\r\n"
        "\r\n"
        "  mrw-cli -clean\r\n"
        "      Clean memory using the areas selected in the current configuration.\r\n"
        "\r\n"
        "  mrw-cli -clean:full\r\n"
        "      Clean all memory areas.");
}

[[nodiscard]] std::wstring get_error_message(DWORD error)
{
    PWSTR buffer = nullptr;
    const DWORD length = FormatMessageW(
        FORMAT_MESSAGE_ALLOCATE_BUFFER
            | FORMAT_MESSAGE_FROM_SYSTEM
            | FORMAT_MESSAGE_IGNORE_INSERTS,
        nullptr,
        error,
        0,
        reinterpret_cast<PWSTR>(&buffer),
        0,
        nullptr);

    std::wstring message;
    if (length != 0 && buffer != nullptr)
    {
        message.assign(buffer, length);
        while (!message.empty()
            && (message.back() == L'\r'
                || message.back() == L'\n'
                || message.back() == L' '))
        {
            message.pop_back();
        }
    }
    LocalFree(buffer);
    return message;
}

void write_windows_error(std::wstring_view prefix, DWORD error)
{
    std::wstring message(prefix);
    const std::wstring detail = get_error_message(error);
    if (!detail.empty())
    {
        message.append(L": ");
        message.append(detail);
    }
    message.append(L"\r\n");

    const int byteCount = WideCharToMultiByte(
        CP_UTF8,
        0,
        message.data(),
        static_cast<int>(message.size()),
        nullptr,
        0,
        nullptr,
        nullptr);
    if (byteCount <= 0)
    {
        return;
    }

    std::string utf8(static_cast<size_t>(byteCount), '\0');
    if (WideCharToMultiByte(
            CP_UTF8,
            0,
            message.data(),
            static_cast<int>(message.size()),
            utf8.data(),
            byteCount,
            nullptr,
            nullptr) > 0)
    {
        write_utf8(GetStdHandle(STD_ERROR_HANDLE), utf8);
    }
}

[[nodiscard]] std::wstring get_executable_directory()
{
    std::vector<wchar_t> buffer(512);
    for (;;)
    {
        const DWORD length = GetModuleFileNameW(
            nullptr,
            buffer.data(),
            static_cast<DWORD>(buffer.size()));
        if (length == 0)
        {
            return {};
        }
        if (length < buffer.size() - 1)
        {
            std::wstring path(buffer.data(), length);
            const size_t separator = path.find_last_of(L"\\/");
            return separator == std::wstring::npos
                ? std::wstring{}
                : path.substr(0, separator);
        }
        buffer.resize(buffer.size() * 2);
    }
}

[[nodiscard]] std::wstring create_pipe_token()
{
    GUID guid{};
    if (FAILED(CoCreateGuid(&guid)))
    {
        return {};
    }

    wchar_t buffer[64]{};
    const int length = StringFromGUID2(guid, buffer, ARRAYSIZE(buffer));
    if (length <= 3)
    {
        return {};
    }

    std::wstring token(buffer + 1, static_cast<size_t>(length - 3));
    return L"Pimnghi.MemReductWinUI.Cli." + token;
}

[[nodiscard]] bool connect_pipe_or_wait_for_exit(
    HANDLE pipe,
    HANDLE process,
    HANDLE pipeEvent)
{
    OVERLAPPED overlapped{};
    overlapped.hEvent = pipeEvent;

    if (ConnectNamedPipe(pipe, &overlapped) != FALSE)
    {
        return true;
    }

    const DWORD error = GetLastError();
    if (error == ERROR_PIPE_CONNECTED)
    {
        return true;
    }
    if (error != ERROR_IO_PENDING)
    {
        return false;
    }

    const HANDLE handles[] = {pipeEvent, process};
    const DWORD waitResult = WaitForMultipleObjects(
        ARRAYSIZE(handles),
        handles,
        FALSE,
        INFINITE);
    if (waitResult == WAIT_OBJECT_0)
    {
        DWORD transferred = 0;
        return GetOverlappedResult(pipe, &overlapped, &transferred, FALSE) != FALSE
            || GetLastError() == ERROR_PIPE_CONNECTED;
    }

    CancelIoEx(pipe, &overlapped);
    return false;
}

[[nodiscard]] std::string read_pipe_message(HANDLE pipe)
{
    std::string message;
    char buffer[1024]{};
    for (;;)
    {
        DWORD bytesRead = 0;
        if (ReadFile(pipe, buffer, sizeof(buffer), &bytesRead, nullptr) == FALSE)
        {
            const DWORD error = GetLastError();
            if (error == ERROR_BROKEN_PIPE)
            {
                break;
            }
            return {};
        }
        if (bytesRead == 0)
        {
            break;
        }
        message.append(buffer, bytesRead);
    }
    return message;
}
}

int wmain(int argc, wchar_t* argv[])
{
    const bool showHelp = argc == 1
        || (argc == 2 && is_help_argument(argv[1]));
    const bool cleanMemory = argc == 2 && is_clean_argument(argv[1]);
    const bool invalidArguments = !showHelp && !cleanMemory;
    const DWORD requestedExitCode = invalidArguments
        ? ExitInvalidOrElevationFailed
        : ExitSuccess;
    HANDLE requestedOutput = requestedExitCode == ExitSuccess
        ? GetStdHandle(STD_OUTPUT_HANDLE)
        : GetStdHandle(STD_ERROR_HANDLE);

    const std::wstring directory = get_executable_directory();
    const std::wstring pipeToken = create_pipe_token();
    if (directory.empty() || pipeToken.empty())
    {
        write_line(
            GetStdHandle(STD_ERROR_HANDLE),
            "Unable to initialize the command-line cleaner.");
        return static_cast<int>(ExitInvalidOrElevationFailed);
    }

    const std::wstring applicationPath = directory + L"\\memreduct-winui.exe";
    if (GetFileAttributesW(applicationPath.c_str()) == INVALID_FILE_ATTRIBUTES)
    {
        if (showHelp || invalidArguments)
        {
            write_fallback_help(requestedOutput);
            return static_cast<int>(requestedExitCode);
        }

        write_line(GetStdHandle(STD_ERROR_HANDLE),
            "memreduct-winui.exe was not found next to mrw-cli.exe.");
        return static_cast<int>(ExitInvalidOrElevationFailed);
    }

    const std::wstring pipePath = L"\\\\.\\pipe\\" + pipeToken;
    unique_handle pipe(CreateNamedPipeW(
        pipePath.c_str(),
        PIPE_ACCESS_INBOUND | FILE_FLAG_OVERLAPPED,
        PIPE_TYPE_BYTE | PIPE_READMODE_BYTE | PIPE_WAIT | PIPE_REJECT_REMOTE_CLIENTS,
        1,
        0,
        16 * 1024,
        0,
        nullptr));
    if (pipe.get() == INVALID_HANDLE_VALUE)
    {
        write_windows_error(L"Unable to create the result channel", GetLastError());
        return static_cast<int>(ExitInvalidOrElevationFailed);
    }

    unique_handle pipeEvent(CreateEventW(nullptr, TRUE, FALSE, nullptr));
    if (pipeEvent.get() == nullptr)
    {
        write_windows_error(L"Unable to create the result event", GetLastError());
        return static_cast<int>(ExitInvalidOrElevationFailed);
    }

    const std::wstring parameters = showHelp || invalidArguments
        ? L"--cli-help --cli-pipe=" + pipeToken
        : std::wstring(argv[1]) + L" --cli-pipe=" + pipeToken;
    SHELLEXECUTEINFOW shellExecuteInfo{
        .cbSize = sizeof(SHELLEXECUTEINFOW),
        .fMask = SEE_MASK_NOCLOSEPROCESS | SEE_MASK_FLAG_NO_UI,
        .hwnd = nullptr,
        .lpVerb = cleanMemory ? L"runas" : nullptr,
        .lpFile = applicationPath.c_str(),
        .lpParameters = parameters.c_str(),
        .lpDirectory = directory.c_str(),
        .nShow = SW_HIDE,
    };
    if (ShellExecuteExW(&shellExecuteInfo) == FALSE
        || shellExecuteInfo.hProcess == nullptr)
    {
        if (showHelp || invalidArguments)
        {
            write_fallback_help(requestedOutput);
            return static_cast<int>(requestedExitCode);
        }

        const DWORD error = GetLastError();
        write_windows_error(L"Unable to start the elevated cleaner", error);
        return static_cast<int>(ExitInvalidOrElevationFailed);
    }
    unique_handle process(shellExecuteInfo.hProcess);

    const bool connected = connect_pipe_or_wait_for_exit(
        pipe.get(),
        process.get(),
        pipeEvent.get());
    const std::string message = connected
        ? read_pipe_message(pipe.get())
        : std::string{};

    WaitForSingleObject(process.get(), INFINITE);
    DWORD exitCode = ExitInvalidOrElevationFailed;
    if (GetExitCodeProcess(process.get(), &exitCode) == FALSE
        || exitCode > ExitInvalidOrElevationFailed)
    {
        exitCode = ExitInvalidOrElevationFailed;
    }
    if (showHelp || invalidArguments)
    {
        exitCode = requestedExitCode;
    }

    HANDLE output = exitCode == ExitSuccess
        ? GetStdHandle(STD_OUTPUT_HANDLE)
        : GetStdHandle(STD_ERROR_HANDLE);
    if (!message.empty())
    {
        write_utf8(output, message);
        if (message.back() != '\n')
        {
            write_utf8(output, "\r\n");
        }
    }
    else
    {
        if (showHelp || invalidArguments)
        {
            write_fallback_help(output);
            return static_cast<int>(exitCode);
        }

        write_line(
            GetStdHandle(STD_ERROR_HANDLE),
            exitCode == ExitCleanupFailed
                ? "Memory cleaning failed."
                : "The elevated cleaner did not return a result.");
        if (exitCode == ExitSuccess)
        {
            exitCode = ExitInvalidOrElevationFailed;
        }
    }

    return static_cast<int>(exitCode);
}
