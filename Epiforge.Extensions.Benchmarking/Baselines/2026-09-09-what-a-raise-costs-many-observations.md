# What a raise costs many observations of one object

*2026-09-09 — `NotificationFanOutBenchmarks`, 1,000 coexisting observations of one object, 1,000 raises*

## The question

An application moving a running interval forward once a second changes one thing and announces three, against every observation attached to that object whatever property each of them watches. Both mechanisms register once with a source's event and keep a list of their own, and they walk that list differently: the fast path looks for the first attachment which wants the name reported and returns before entering a propagation where none does, while the graph invokes every attachment's handler and each of them decides for itself and returns.

The prediction recorded before the first run was that this would favour the fast path by a low single-digit multiple on a raise nobody wants. It was wrong in direction and by two orders of magnitude, and the correction after the first run — that the distinct arms would isolate dispatch — was wrong as well. Both misses have the same cause and it is worth more than the arms are.

## The measurement

Every figure is 1,000 raises against 1,000 observations. The subject passes pre-allocated notification arguments, so a raise allocates nothing of its own.

| arm | fast path | graph | graph faster by |
|---|---:|---:|---:|
| one expression, watched property | 5,050.9 μs | 24.3 μs | **207x** |
| one expression, property nobody watches | 3,564.0 μs | 6.3 μs | **565x** |
| one expression, three-property advance | 8,670.7 μs | 33.5 μs | **259x** |
| distinct constants, watched property | 10,114.6 μs | 5,460.5 μs | **1.85x** |
| distinct constants, property nobody watches | 3,500.8 μs | 6.3 μs | **552x** |
| distinct constants, three-property advance | 13,906.9 μs | 5,515.0 μs | **2.52x** |

The fast path allocates nothing on every arm. The graph allocates 24,000 B on each arm which re-evaluates, which is 24 bytes per raise.

## What dominates is sharing, not dispatch

The graph caches a node per expression. A thousand observations of one expression over one object are served by one graph with one attachment, so a raise walks a list of one where the fast path, which caches nothing across observations, walks a thousand. That is the first three rows, and it is the cache being measured rather than the dispatch.

Giving every observation its own constant was meant to stop that. **It did not, and the allocation figures say so exactly.** The graph allocates 24,000 B on the distinct watched arm — the same 24,000 B as on the shared one, which is 24 bytes per raise and not per observation. One boxed value per raise means one member node reading the property, shared across all thousand observations, because `ticker.End` is the identical subexpression in all thousand expressions and only the comparison above it differs. Had each observation had its own read there would have been a thousand boxes per raise and twenty-four million bytes.

So the distinct arms do not compare a thousand attachments against a thousand either. They compare a thousand whole-delegate invocations against one shared property read feeding a thousand comparison nodes.

## The finding, which is not the one this instrument was built for

**Where many observations read one source, the graph shares the read and the fast path repeats it.** That is the mirror of the held-slot problem one level up: within an observation, holding a subexpression stops the fast path recomputing what cannot change; across observations, nothing stops it recomputing what another observation has already read.

On the watched arm with genuinely distinct downstream work the graph is 1.85x faster, and on the three-property advance 2.52x. Those are the honest numbers for fan-out over a single hot object, and they are the first shape measured in this whole effort where the fast path loses.

The dispatch difference the instrument was built for cannot be read from it, because no arm gives the graph more than one attachment on the source. What can be read is the fast path's own per-attachment cost on a raise it does not want: 3.5 ns, walked a thousand times.

## What it means for an application

Two shapes want different mechanisms, and `UseDirectSubscription` is an option on the observer rather than a property of the library, so an application which has both can run two observers.

- Many observations over **distinct** objects, each reading its own — a grid of cells over a row of entities — is the shape every other instrument here measured, and the fast path wins it by 6.56x in memory and 23.17x in time at construction, and allocates nothing per notification against the graph's 24.9 bytes.
- Many observations over **one** object, all reading the same property of it — a clock, a running interval, a selection — is this shape, and the graph wins it by 1.85x to 2.52x, or by two orders of magnitude when the expressions are identical.

## Predictions scored

**Both were wrong.** The first said the fast path would win a raise nobody wants by a low single-digit multiple; the graph won by 565x. The second said giving every observation its own constant would isolate dispatch; it isolated one level of sharing and left another in place.

Both misses have one cause: reasoning carefully about the layer being examined — the two registries, then the node cache for the comparison — while a layer above or below decided the result. That is the third time in this effort, and the correction is not to reason more carefully about a layer but to find the number which discriminates between the explanations before believing any of them. Here that number was the allocation, which distinguishes one shared read from a thousand in a single figure, and it was in the table both times.
