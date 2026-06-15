namespace LocalLLM.Core;

public sealed class AssistantOptions
{
    public string LogRootPath { get; set; } = @"C:\Users\jkhong\Documents\MHBrakeDisk_Eonyang\LogFile";

    public string IndexRootPath { get; set; } = "LLMIndex";

    public bool UseIndexFirst { get; set; } = true;

    public string[] EventKeywords { get; set; } =
    [
        "Start",
        "Inspection",
        "검사",
        "시작"
    ];

    public string[] FailureKeywords { get; set; } =
    [
        "Error",
        "Fail",
        "Failed",
        "Reject",
        "Rejected",
        "NG",
        "Warning",
        "Alarm",
        "Exception",
        "Timeout",
        "NotReady",
        "Not Ready",
        "실패",
        "거부",
        "알람",
        "오류",
        "에러",
        "타임아웃"
    ];

    public Dictionary<string, string[]> InspectionAliases { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["WheelAssembly"] = ["WheelAssembly"],
        ["PadOutboard"] = ["PadOutboard", "Outboard", "Pad O/B", "PadOB"],
        ["MaxDiameterVent"] = ["MaxDiameterVent"],
        ["WheelOuterDiameter"] = ["WheelOuterDiameter", "OT", "Ot", "외경", "Outer"],
        ["HubSurfaceDark"] = ["HubSurfaceDark", "Hub Dark"],
        ["HubSurfaceBright"] = ["HubSurfaceBright", "Hub Bright"],
        ["PadInboard"] = ["PadInboard", "Inboard", "Pad I/B", "PadIB"]
    };

    public Dictionary<string, string[]> RecommendedActions { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["IoError"] =
        [
            "FnIO 모듈 연결 상태를 확인하세요.",
            "출력 보드 전원과 통신 케이블 상태를 확인하세요.",
            "동일 시간대 PLC/IO 통신 로그가 있는지 확인하세요."
        ],
        ["InspectionCompleteFalse"] =
        [
            "해당 검사 Area의 이미지, 조명, 제품 위치를 확인하세요.",
            "Recipe와 검사 임계값 설정이 정상인지 확인하세요.",
            "False가 특정 Grab Index에 집중되는지 확인하세요."
        ],
        ["Timeout"] =
        [
            "대기 중인 센서 또는 PLC 신호가 정상 전환되는지 확인하세요.",
            "외부 장치 응답 시간과 통신 상태를 확인하세요."
        ],
        ["Default"] =
        [
            "원본 로그의 첫 발생 전후 10초 구간을 추가 확인하세요."
        ]
    };

    public Dictionary<string, InspectionRule> InspectionRules { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["WheelOuterDiameter"] = new InspectionRule
        {
            DisplayName = "외경 검사",
            FailureMeaning = "외경 검사 결과가 NG로 판정되었습니다.",
            ProbableCauses =
            [
                "제품 안착 위치가 기준 위치에서 벗어났을 수 있습니다.",
                "외경 검사 Threshold 설정이 맞지 않을 수 있습니다.",
                "조명 조건이 변경되었을 수 있습니다."
            ],
            RecommendedActions =
            [
                "제품 안착 상태를 확인하세요.",
                "외경 검사 Recipe 값을 확인하세요.",
                "조명 밝기와 카메라 이미지를 확인하세요."
            ]
        }
    };

    public Dictionary<string, ErrorRule> ErrorRules { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["FnIO read output bit fail"] = new ErrorRule
        {
            DisplayName = "IO 출력 비트 읽기 실패",
            Impact = "기기 동작 정지 가능성이 높은 IO 통신 오류입니다.",
            ProbableCauses =
            [
                "FnIO 모듈 통신 이상",
                "출력 보드 전원 이상",
                "PLC/IO 케이블 접촉 불량"
            ],
            RecommendedActions =
            [
                "FnIO 모듈 전원 상태를 확인하세요.",
                "PLC 통신 상태를 확인하세요.",
                "IO 케이블 연결 상태를 확인하세요."
            ]
        },
        ["Load PLC Fail"] = new ErrorRule
        {
            DisplayName = "PLC 로드 실패",
            Impact = "PLC 상태 읽기 또는 로딩 시퀀스 실패 가능성이 있습니다.",
            ProbableCauses =
            [
                "PLC 통신 이상",
                "로딩 장치 상태 이상",
                "PLC 응답 지연"
            ],
            RecommendedActions =
            [
                "PLC 통신 상태를 확인하세요.",
                "LoadingThread 전후 로그를 확인하세요.",
                "로딩 장치 인터락 상태를 확인하세요."
            ]
        }
    };

    public EventPatternRule[] EventPatterns { get; set; } =
    [
        new EventPatternRule
        {
            Type = "InspectionComplete",
            Regex = @"On\s+(?<station>Station\d+)\s+InspectionComplete\s*-\s*(?<inspection>[^,]+),\s*(?<result>True|False)",
            Module = "InspectionProcessor"
        },
        new EventPatternRule
        {
            Type = "ProcessStart",
            Regex = @"Process Start SN\s*:\s*(?<serialNumber>.*?),\s*Area\s*:\s*(?<inspection>[^,]+),\s*Grab Idx\s*:\s*(?<grabIndex>\d+)",
            Module = "InspectionProcessor"
        },
        new EventPatternRule
        {
            Type = "IoError",
            Module = "FnIO",
            Contains = ["FnIO Error"],
            MessageAfter = "FnIO Error!:"
        },
        new EventPatternRule
        {
            Type = "StationStart",
            Regex = @"(?<station>Station\d+)\s+Start\s*-\s*(?<serialNumber>.*?)(?:,|$)"
        },
        new EventPatternRule
        {
            Type = "ResetStart",
            Regex = @"(?<station>Station\d+)\s+Reset\s+Start"
        },
        new EventPatternRule
        {
            Type = "ResetComplete",
            Regex = @"(?<station>Station\d+)\s+Reset\s+Complete"
        },
        new EventPatternRule
        {
            Type = "Timeout",
            Contains = ["Timeout"]
        },
        new EventPatternRule
        {
            Type = "WaitSignal",
            Contains = ["Wait"]
        },
        new EventPatternRule
        {
            Type = "Alarm",
            Contains = ["Alarm"]
        }
    ];

    public static AssistantOptions CreateDefault() => new();

    public AssistantOptions WithLogRootPath(string logRootPath)
    {
        return new AssistantOptions
        {
            LogRootPath = logRootPath,
            IndexRootPath = IndexRootPath,
            UseIndexFirst = UseIndexFirst,
            EventKeywords = EventKeywords,
            FailureKeywords = FailureKeywords,
            InspectionAliases = InspectionAliases,
            RecommendedActions = RecommendedActions,
            InspectionRules = InspectionRules,
            ErrorRules = ErrorRules,
            EventPatterns = EventPatterns
        };
    }
}
