### SelfTestAdapter

![DocDown.Tool Structure](DocDownToolView.svg)

#### Purpose

`SelfTestAdapter` maps Core's dependency-free self-test records into the
`DemaConsulting.TestResults` object model. It is the reason `DemaConsulting.TestResults` is referenced
by the tool and nowhere else: Core defines its self-test contract as plain records with no NuGet
dependency, and the mapping into the serializable results model lives here, on the tool side of the
boundary.

#### Data Model

`SelfTestAdapter` is an `internal static class` with no state. It records a fixed code base
(`DemaConsulting.DocDown.Tool`) on every mapped result.

#### Key Methods

- **`TestOutcome ToTestOutcome(SelfTestStatus status)`** — maps `Passed` to `Passed`, `Failed` to
  `Failed`, and `Skipped` to `NotExecuted`. The skip mapping is the load-bearing one: a not-executed
  outcome is distinct from a failure, so a traceability pipeline ignores it as evidence rather than
  counting it as a fail. An unknown status is rejected with `ArgumentOutOfRangeException`.
- **`TestResult ToTestResult(SelfTestCase testCase, SelfTestResult result)`** — builds a result
  carrying the case name, the case category as the class name, the tool's code base, the machine
  name, the run duration, the mapped outcome, and any skip reason or failure detail as the error
  message. Both arguments are validated as non-null.

#### Error Handling

The methods are pure and total over valid inputs. A null case or result is rejected with
`ArgumentNullException`; an unrecognized status is rejected with `ArgumentOutOfRangeException`. There
is no I/O and no other failure mode.

#### Dependencies

- **DocDown.Core** — `SelfTestCase`, `SelfTestResult`, and `SelfTestStatus`.
- **DemaConsulting.TestResults** — `TestResult` and `TestOutcome`.

#### Callers

`Validation` calls the adapter for each engine self-test result to build the collection it serializes.
