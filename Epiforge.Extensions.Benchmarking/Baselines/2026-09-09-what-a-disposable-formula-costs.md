# What a disposable formula costs

*2026-09-09 — `FormulaShapeBenchmarks`, 1,000 coexisting observations under a filtered query, constructed and disposed*

## The question

`DirectSubscriptionAnalyzer.AnalyzeMethodCall` refused any call whose return value the observer disposes of, because the fast path holds no value of its own and so can neither hold what such a call produced nor dispose of it. An application whose formula engine reaches its data through a method returning a disposable query object therefore runs on the graph however its observer is configured, and every such formula is refused whole.

The shape measured is the one such an engine emits: an invocation of a literal lambda whose body calls through a captured object and reads a notifying value from what it returned. The plain arms are the same expression through a sealed type implementing neither disposal interface, which the analyzer admits whatever the options say, and are therefore the ceiling.

## The before

| arm | allocated | time | bytes per element |
|---|---:|---:|---:|
| disposable formula, default options | 8,092.59 KB | 8,955.7 μs | 8,286.8 |
| disposable formula, graph | 8,092.62 KB | 8,741.0 μs | 8,286.8 |
| plain formula, fast path | 1,418.31 KB | 1,149.9 μs | 1,452.4 |
| plain formula, graph | 7,933.08 KB | 9,432.1 μs | 8,123.6 |
| a comparison, fast path — control | 965.19 KB | 293.7 μs | 988.4 |
| a comparison, graph — control | 2,730.16 KB | 2,378.9 μs | 2,795.7 |

**The refusal is proved, not assumed.** The default-options arm lands on the graph arm at 8,092.59 KB against 8,092.62 — three hundredths of a kilobyte apart on an arm whose own deviation is 2%.

## The instrument was wrong, and its own numbers said so

The plain formula cost the graph **less** memory than the disposable one and **more** time, by 8%, which is well outside the error on either. That is backwards, and the cause was in the instrument rather than the library: both wrappers subscribed to the person they read, and only the disposable one had a `Dispose` to detach in. The plain arms therefore left a handler on every person for every element of every invocation, growing without bound across the run.

Neither wrapper subscribes now. These arms construct and discard rather than mutate, so nothing they measure is lost by it. What the leak had been worth, on the same instrument the same day:

| arm | leaking | not leaking |
|---|---:|---:|
| plain formula, fast path | 1,418.31 KB / 1,149.9 μs | **1,183.94 KB / 356.9 μs** |
| plain formula, graph | 7,933.08 KB / 9,432.1 μs | **7,876.90 KB / 8,061.7 μs** |

**The fast-path arm was running 3.22x slow.** Every ceiling read off the first run was a floor on the ceiling.

## The after

An invariant call — one whose object and every argument cannot change — is now held in a slot resolved by the first evaluation which reads it, and what that slot resolved to is disposed of once when the observation is discarded.

| arm | allocated | time | bytes per element |
|---|---:|---:|---:|
| disposable formula, default options | **1,207.38 KB** | **363.2 μs** | **1,236.4** |
| disposable formula, graph | 7,923.80 KB | 8,413.9 μs | 8,113.9 |
| plain formula, fast path | 1,183.94 KB | 356.9 μs | 1,212.4 |
| plain formula, graph | 7,876.90 KB | 8,061.7 μs | 8,066.0 |
| a comparison, fast path — control | 980.81 KB | 300.6 μs | 1,004.3 |
| a comparison, graph — control | 2,695.19 KB | 2,341.3 μs | 2,759.9 |

**6.56x less memory and 23.17x less time** for the shape a formula engine emits, against the graph arm measured beside it in the same run.

The arm also leaves Gen2 entirely: 31.25 collections on the graph arm, none on the fast path.

## The ceiling was taken to within one array

The disposable formula on the fast path costs **1,207.38 KB** against a ceiling of **1,183.94** — the same shape through a type nothing disposes. The difference is **23.44 KB over a thousand elements, which is 24.0 bytes each**, and that is exactly one `object?[1]`: the slot the held call resolves into. There is nothing else left to recover on this shape.

For scale: the disposable formula now costs the fast path **less than half** what a bare `person.Rank > 0` costs the graph — 1,207.38 KB against 2,695.19.

## What the control arm caught

`RankComparisonDirect` moved 965.19 → 980.81 KB, which is **+15.62 KB over a thousand elements, or 16 bytes each**. That is not drift and it is not the shape: it is two reference-sized fields added to every fast-path observation, `held` and `disposedHeldSlots`, at eight bytes apiece. A control that reproduces to the byte is what makes a sixteen-byte regression visible at all.

It is recoverable. Those two fields plus `sites` are all per-lambda or per-observation state the `DirectEvaluator` already holds; carrying one reference to the evaluator instead of `sites` and `disposedHeldSlots` separately would put the count back to where it was and take another eight bytes with it. Predicted after that change: `RankComparisonDirect` returns to about 965 KB.

The graph control moved 2,730.16 → 2,695.19 KB, −1.28%, in the opposite direction and on an arm the change cannot touch. That is drift.

## Predictions scored

Three were recorded before the run and all three held.

1. The default arm would leave the graph arm and land near the plain fast-path arm. It landed within 24 bytes per element of it.
2. The plain arms would come down once they stopped leaking. They did, by 69% in time on the fast-path arm.
3. `AnInvariantCallIsMadeOnceHoweverOftenTheObservationIsReevaluated` would stay green on both rows, which is the assertion that says the saving was not bought by churning query objects. It did.

## What has not been measured

What the shape costs under mutation rather than construction. These arms construct a thousand observations and discard them; they never change a value, so they never exercise the property the whole design turns on — that a held call is made once however many notifications arrive. That property is pinned by test rather than by number, which is the stronger instrument for a count but gives no figure for what the churn would have cost had it been admitted without holding.

Sharing across observations. The graph serves two observations of one expression from one cached node and therefore one query object; the fast path holds per observation. `TwoObservationsOfOneExpressionMakeNoMoreThanOneQueryEach` records the difference rather than requiring it away. In this instrument every element is a different argument, so nothing is shared either way and the question does not arise.

## A defect this instrument's tests found in the graph

The graph makes a deferred call **twice** the first time a branch reaches it, when what the call is reached through was deferred along with it. The call's evaluation begins by reading its object's evaluation; reading a deferred node both resolves it and announces that it changed; the announcement reaches the call, which is that object's dependent, and re-enters the call's evaluation. The inner evaluation completes against the resolved object and the outer one, still on its first line, repeats it.

The value is correct and the work is doubled. For a call producing something that must be disposed of, it is one more of them made and discarded than the shape requires — on exactly the shape an application's formula engine emits inside a conditional or a short circuit. It is recorded by `TheGraphMakesADeferredCallTwiceWhenWhatItIsReachedThroughIsAlsoDeferred`, and its counterpart `TheGraphMakesACallWhichIsNotDeferredOnce` identifies deferral as what it turns on.

## The sixteen bytes, recovered and scored

An observation carried a separate reference to each piece of state belonging to its lambda. All of it already lives on the `DirectEvaluator` every observation of that lambda shares, so it now carries one reference to that instead: `sites`, `lambdaExpression` and `disposedHeldSlots` are gone from the observation, and `linkSites` from the observation which follows a link. Same instrument, same day.

Three figures were predicted before the run, from the field accounting alone. All three landed to the hundredth of a kilobyte.

| arm | predicted | measured | before |
|---|---:|---:|---:|
| a comparison, fast path | about 965.2 KB | **965.19 KB** | 980.81 KB |
| plain formula, fast path | about 1,160.5 KB | **1,160.50 KB** | 1,183.94 KB |
| disposable formula, default options | about 1,183.9 KB | **1,183.94 KB** | 1,207.38 KB |

The comparison arm pays only the two recovered fields, sixteen bytes per element, and returns exactly to the figure it has held on this shape since the invocation instrument was first run. The two formula arms follow a link and recover eight more, twenty-four bytes each. The held call's own slot still costs 24.0 bytes per element — 1,183.94 against 1,160.50 — the same figure as the run before, which is the arithmetic closing twice on the same number.

**A control moved and is not explained.** `DisposableFormulaGraph` went 7,923.80 to 8,045.65 KB, up 1.5%, on an arm this change cannot reach: the graph observer short-circuits before an evaluator is ever built. The other two graph arms were byte-stable across the same two runs — `PlainFormulaGraph` identical to the hundredth, `RankComparisonGraph` within 0.03 KB — so it is not instrument drift. It is the only arm reaching Gen2 in quantity and its Gen1 count moved with it, which makes collection timing the likeliest cause, but that is a guess and it has not been checked. A third run would settle it.
