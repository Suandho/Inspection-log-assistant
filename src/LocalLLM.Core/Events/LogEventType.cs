namespace LocalLLM.Core;

public enum LogEventType
{
    Unknown,
    StationStart,
    ResetStart,
    ResetComplete,
    ProcessStart,
    InspectionComplete,
    IoError,
    Timeout,
    WaitSignal,
    Alarm,
    Error
}
