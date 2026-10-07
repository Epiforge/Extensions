# The let the graph was building

*2026-10-06 — `InvocationLetChangeBenchmarks` and `InvocationLetConstructionBenchmarks`, 1,000 observations of a duration format per operation*

## The question

A formula engine emits `Expression.Invoke(literalLambda, argument)` as a let. Its duration format reads its parameter eight times, three of them in conditional branches. `InvocationReducer` substitutes an argument only where its parameter is read exactly once and not where the body might not evaluate it, so this invocation survived reduction, the analyzer refused it, and the whole observation built the graph. The question is what binding it as a let on the fast path is worth, and whether it costs anything against writing the substitution out by hand.

Three arms each: the invocation on the graph (the baseline), the invocation under the default options, and the format's body with every read of the parameter replaced by a read of the duration, which the fast path already served.

## Before

| arm | change | allocated | construction | allocated |
|---|---:|---:|---:|---:|
| invocation, graph | 52,889.6 μs | 24,875.04 KB | 26,377.9 μs | 34.69 MB |
| invocation, default options | 61,527.7 μs | 24,914.1 KB | 27,489.8 μs | 34.58 MB |
| written out by hand, default options | 214.5 μs | 188.57 KB | 400.9 μs | 1.03 MB |

**The default-options arm lands on the graph arm in both instruments**, which is the refusal proved rather than assumed.

## After

| arm | change | allocated | construction | allocated |
|---|---:|---:|---:|---:|
| invocation, graph | 48,509.7 μs | 26,006.65 KB | 27,995.2 μs | 34.61 MB |
| invocation, default options | **207.9 μs** | **188.57 KB** | **401.9 μs** | **1.03 MB** |
| written out by hand, default options | 212.4 μs | 188.57 KB | 397.1 μs | 1.03 MB |

**The let lands on the hand-written arm**, allocating the same 188.57 KB to the hundredth of a kilobyte for a thousand changes and the same 1.03 MB for a thousand constructions. It changes 2% faster than the hand-written form, which reads the duration's property eight times where the let reads it once, and constructs within 1.2% of it.

## What it is worth

| | time | memory |
|---|---:|---:|
| a change, graph against let | **233x** (48.5 μs against 208 ns) | **138x** (26,631 B against 193 B) |
| a construction, graph against let | **69.7x** (28.0 μs against 402 ns) | **33.6x** |

Against its own before, the default-options change went from 61,527.7 μs to 207.9 μs, 296x.

## Predictions scored

All held. The let was predicted within ±10% of the hand-written arm in time and at its allocation in both instruments; it came in at 0.98x and 1.01x and allocated identically. The graph arms were predicted not to move and stayed within their run-to-run spread (StdDev 5.8% and 6.2%), allocation within 4.5% on the change and 0.2% on construction. Every test passed on every framework: Expressions 1,072 and Expressions without dynamic code 1,071, eleven new agreement tests among them, the twelfth confirming that a body the analyzer refuses still builds the graph.

## What has not been measured

The let without dynamic code, where the evaluation runs through closures rather than compiled code; the agreement tests cover it, the benchmarks do not. A let whose body reads through its argument, where the fast path follows the argument's value as a link and so takes the linking observation's cost per change.

## After the narrowing

Because the graph builds the body again whenever any argument changes, a let is now admitted only where its body subscribes to nothing that could notify, or only through the one argument it binds and outside any operand it defers. The duration format subscribes to nothing in its body, so it stays on the fast path. Same instruments, same evening.

| arm | change | allocated | construction | allocated |
|---|---:|---:|---:|---:|
| invocation, graph | 47,859.9 μs | 26,006.65 KB | 27,669.1 μs | 34.61 MB |
| invocation, default options | **206.2 μs** | **188.57 KB** | **388.5 μs** | **1.03 MB** |
| written out by hand, default options | 213.1 μs | 188.57 KB | 389.2 μs | 1.03 MB |

Every arm is within its run-to-run spread of the run above and allocates identically, as predicted. Tests: Expressions 1,075 and Expressions without dynamic code 1,074 on every framework, the three added covering a branch of the body that subscribes, a body following one of two arguments, and a faulting argument releasing what the body followed.
