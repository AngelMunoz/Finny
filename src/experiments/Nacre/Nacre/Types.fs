module Nacre.Types

open Microsoft.Playwright

type ExecutionSuite =
    | All
    | ExecutionSuite of Files: string array * Groups: string array


type BrowserEnum =
    | Chromium = 0
    | Chrome = 1
    | Edge = 2
    | Firefox = 3
    | Webkit = 4

type Browser =
    | Chromium
    | Chrome
    | Edge
    | Firefox
    | Webkit

    static member FromBrowserEnum(be: BrowserEnum) : Browser =
        match be with
        | BrowserEnum.Chromium -> Chromium
        | BrowserEnum.Chrome -> Chrome
        | BrowserEnum.Edge -> Edge
        | BrowserEnum.Firefox -> Firefox
        | BrowserEnum.Webkit -> Webkit
        | _ -> failwith "Unsuported value"


type TestRunnerOptions =
    { executionList: ExecutionSuite
      browsers: Browser array
      testsDirectory: string
      serveDirectory: string option }

[<Struct>]
type TestError =
    { actual: obj
      expected: obj
      message: string
      name: string
      stack: string }

[<Struct>]
type Test =
    { name: string
      passed: bool
      duration: int
      error: TestError voption }

[<Struct>]
type Suite =
    { name: string option
      suites: Suite array option
      tests: Test array option }

[<Struct>]
type ExecutionResult =
    { logs: obj seq
      errors: TestError seq
      passed: bool
      testResults: Suite }

type SendMessagePayload =
    { ``type``: string
      sessionId: string option
      testFile: string option
      userAgent: string option
      result: ExecutionResult voption
      testResults: ExecutionResult voption }
