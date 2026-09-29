# The release beside it

*2026-09-28 — twelve benchmark classes run with 7.0.0 itself, restored from NuGet, as a second job in the same run, and the footprint report widened to NMF Expressions, ObservableComputations and the copies an observation keeps*

## How this was measured

The Benchmarking project builds a second job against the published package when a class carries `[AgainstReleasedExpressions("7.0.0")]`, so every 7.0.0 row below ran on the same machine, in the same run and under the same job as the 7.0.1 row beside it. Nothing in this baseline compares across runs except the one section that says so.

- `BenchmarkRun-20260928-182451` ran ten classes: `BlockingAsyncDisposal`, `DictionaryPropagation`, `MembershipChangeComparison`, `ObserveDictionaryWhere`, `ObservedSequenceCopy`, `ObserveSelectMany`, `OperatorConstruction`, `QueryBuilding`, `QueryLambdaReuse` and `SynchronizedQuery`. All but `MembershipChangeComparison`, which compares libraries rather than releases, carry the 7.0.0 job.
- Reading it found two costs of 7.0.1's own making, both removed below. `BenchmarkRun-20260928-201715` then ran `ObservedDictionaryCopy`, new, and `QueryBuilding` again.
- The footprint report was taken at 20:29 on the code of that second run.

Tests after both changes: Expressions 982 of 982, Comparisons 48 of 48, Collections 647 of 647, Components 194 of 194, on each of five frameworks.

## What the first run found

- **Enumerating the copy of an empty sequence allocated 40 B that 7.0.0 did not.** An observation's copy is a `List<T>`, and `kept.GetEnumerator()` boxes the list's struct enumerator; 7.0.0 enumerated the sequence through its interface, which on .NET 8 and later answers an empty list with a cached enumerator. This is what the first run's building figures over concatenations were made of: a build over a concatenation of two, four or eight empty observations enumerated each of them, so it allocated 96, 176 and 336 B more than 7.0.0, which is 40 B per observation plus 16. The five collection observations and the dictionary observation now enumerate their copies through the interface, and the second run allocates what 7.0.0 did for every enumeration.
- **Nothing in the suite had changed a key of an observed dictionary**, which is where 7.0.1's copy and its reconciliation of each key a notification names do their work; `DictionaryPropagation` changes values' properties and `ObserveDictionaryWhere` reads. `ObservedDictionaryCopy` now does. In the sandbox the reconciliation took 3.1x 7.0.0's time and allocated a list of keys, lists of results and fresh event arguments for every change. An observation now applies a change naming one key directly, announcing the dictionary's own event arguments, when the dictionary and the copy agree it is the change still outstanding for that key, which they do whenever notifications arrive in the order of the changes; anything else is reconciled as before. Removing the check against the dictionary from that path fails exactly the two rows of `ARemoveNotifiedBeforeTheAddItFollowsLeavesTheQueryAgreeing`.

## What 7.0.1 costs against 7.0.0

### Observing a sequence

| arm, `ObservedSequenceCopy` | 7.0.0 | 7.0.1 | |
|---|---:|---:|---:|
| insert and remove at the end, unobserved | 17.07 ns / 144 B | 17.41 ns / 144 B | 1.02x |
| the same, observed | 28.52 ns / 144 B | **49.04 ns** / 144 B | 1.72x |
| the same, filtered | 598.33 ns / 1,416 B | **631.67 ns** / 1,416 B | 1.06x |
| insert and remove at the front of ten thousand, unobserved | 1,464.44 ns / 144 B | 1,463.99 ns / 144 B | 1.00x |
| the same, observed | 1,485.68 ns / 144 B | **3,832.59 ns** / 144 B | 2.58x |
| the same, filtered | 2,352.46 ns / 1,416 B | **4,650.56 ns** / 1,416 B | 1.98x |

**An observed list pays 20.5 ns more for an insertion and a removal at the end**, which is the observation's lock and its copy, and `SynchronizedQuery`'s `Observed` arm agrees: 50.39 ns against 31.53. At the front of ten thousand elements the copy moves its elements as the list does, 2,347 ns more, 1.60x what the list itself spends. Allocation is unchanged on every row.

### Observing a dictionary

| arm, `ObservedDictionaryCopy`, ten thousand keys | 7.0.0 | 7.0.1 | |
|---|---:|---:|---:|
| add a key and remove it, unobserved | 13.13 ns / 0 B | 13.38 ns / 0 B | 1.02x |
| the same, observed | 87.30 ns / 592 B | **72.57 ns / 144 B** | 0.83x |
| the same, filtered | 439.79 ns / 1,568 B | **416.42 ns / 1,120 B** | 0.95x |
| the same on an `ObservableConcurrentDictionary`, unobserved | 34.25 ns / 40 B | 34.06 ns / 40 B | 0.99x |
| the same, observed | 104.12 ns / 632 B | **92.91 ns / 184 B** | 0.89x |
| replace a value and restore it, unobserved | 9.93 ns / 0 B | 9.85 ns / 0 B | 0.99x |
| the same, observed | 97.22 ns / 880 B | **70.66 ns / 208 B** | 0.73x |
| the same, filtered | 763.47 ns / 3,008 B | **709.04 ns / 2,336 B** | 0.93x |

**Observing a dictionary costs less per change than it did.** 7.0.0's observation subscribed to every notification a dictionary raises, the generic, the boxed and the collection change and both property notifications, which made the dictionary build every one of their arguments; 7.0.1's listens to the generic notification alone when the dictionary raises it. The observed arms allocate 144, 184 and 208 B, a quarter of what they did, which is what the dictionary's arguments for its generic notification alone would account for.

### Building

| arm, `OperatorConstruction`, over an empty collection | 7.0.0 | 7.0.1 | |
|---|---:|---:|---:|
| `Root` | 39.72 μs / 146 KB | 37.07 μs / 130 KB | 0.93x |
| `Cast` | 55.35 μs / 166 KB | 62.42 μs / 170 KB | 1.13x |
| `Select` | 55.50 μs / 166 KB | 63.07 μs / 170 KB | 1.14x |
| `Where` | 53.66 μs / 166 KB | 60.52 μs / 170 KB | 1.13x |
| `Concat` | 50.86 μs / 128 KB | 58.59 μs / 134 KB | 1.15x |
| `SelectMany` | 119.36 μs / 326 KB | 135.54 μs / 346 KB | 1.14x |
| `Slice`, a chain of seven | 708.46 μs / 1,800 KB | 827.02 μs / 1,924 KB | 1.17x |

Over one element the same arms moved 0.95x (`Root`), 1.06x to 1.08x (`Cast`, `Select`, `Where`, `Slice`, `SelectMany`) and 1.14x (`Concat`). **A slice costs 7,696 B and 3.23 μs where it cost 7,200 B and 2.77 μs.** A filtered, projected or cast view allocates 16 B more.

| arm, `QueryBuilding`, second run, 256 builds | 7.0.0 | 7.0.1 | |
|---|---:|---:|---:|
| a filtered view over an observation, one lock | 50.86 μs / 166 KB | 56.68 μs / 170 KB | 1.11x |
| over another filtered view, one lock | 57.28 μs / 182 KB | 63.52 μs / 186 KB | 1.11x |
| over a concatenation of two, three locks | 53.24 μs / 180 KB | 72.83 μs / 184 KB | 1.37x |
| of four, seven locks | 61.47 μs / 208 KB | 102.06 μs / 212 KB | 1.66x |
| of eight, fifteen locks | 77.00 μs / 264 KB | 168.43 μs / 268 KB | 2.19x |
| enumerating a concatenation of two | 3.19 μs / 14 KB | 3.08 μs / 14 KB | 0.97x |
| enumerating a concatenation of eight | 23.51 μs / 98 KB | 21.57 μs / 98 KB | 0.92x |

**Building costs 16 B more whatever it holds, and time which grows with the locks it holds**: about 5.8 μs over 256 builds with one, 19.6 with three, 40.6 with seven and 91.4 with fifteen, roughly 23 to 27 ns a build for each lock beyond the first. The first run, before the enumerator change, had the same times and allocated 204, 252 and 348 KB over the concatenations and 34 and 178 KB enumerating them.

`QueryLambdaReuse`'s arms built in 1.03x to 1.10x their 7.0.0 time and allocated 4 to 66 KB more; `WhereShared` 135.87 μs against 128.44, `WhereRebuilt` 14,413 μs against 14,786 with an error of 3,153.

### Flattening

| arm, `ObserveSelectMany` | 100 teams | 1,000 | 10,000 |
|---|---:|---:|---:|
| a change to an inner sequence | 119.7 → 139.2 ns, 1.16x | 178.0 → 204.6 ns, 1.15x | 219.8 → 254.6 ns, 1.16x |
| reading by index | 1.08x | 1.07x | 1.10x |
| enumerating | 1.00x | 1.00x | 0.97x |

Allocation is unchanged. The copies each announcing inner sequence keeps cost the change 15% to 16% and a read by index 7% to 10%.

### Synchronized queries

| arm, `SynchronizedQuery`, an addition and a removal at the end of a thousand | 7.0.0 | 7.0.1 | |
|---|---:|---:|---:|
| unobserved | 20.23 ns / 192 B | 19.33 ns / 192 B | 0.96x |
| observed | 31.53 ns / 192 B | 50.39 ns / 192 B | 1.60x |
| `ObserveUsingSynchronizationCallback` | 115.21 ns / 720 B | 224.59 ns / **528 B** | 1.95x |
| `ObserveUsingSynchronizationContext` | 101.74 ns / 720 B | 194.36 ns / **528 B** | 1.91x |
| the same, from the context's own thread | 91.62 ns / 592 B | 189.34 ns / **528 B** | 2.07x |
| `ObserveUsingSyncRoot` | 93.94 ns / 528 B | 214.95 ns / 528 B | 2.29x |
| `ObserveUsingSynchronizationContextEventually` | 96.79 ns / 720 B | 132.16 ns / 736 B | 1.37x |

**Applying a change through a synchronized query that is not eventual takes 1.9x to 2.3x as long**, the price of queueing each change and applying the queue once the thread holds no change lock, which is what ended their deadlocks. The delegates the deferral uses are cached, so they allocate less than 7.0.0's.

### Blocking disposal

| arm, `BlockingAsyncDisposal`, a thousand values replaced | 7.0.0 | 7.0.1 |
|---|---:|---:|
| unobserved | 9.89 μs / 46.88 KB | 9.03 μs / 46.88 KB |
| an async value, waited for | 74.98 μs / 117.19 KB | 73.02 μs / 117.19 KB |
| an async value, not waited for | 485.52 μs / 320.32 KB | 488.26 μs / 320.31 KB |
| a synchronous value | 74.51 μs / 117.19 KB | 72.36 μs / 117.19 KB |

**Disposal on a thread with no context and the default scheduler costs what it did.** An earlier cut of 7.0.1 began every blocking disposal on the thread pool, which in the sandbox cost 203 B and about six times the time per disposal; that cut never reached a release.

### Dictionaries' values and reads

`DictionaryPropagation` ran 0.97x to 1.05x of 7.0.0 and allocated the same on every arm but `ChangeEveryValueInAnAllQuery`, 129.07 μs against 118.92, 1.09x. `ObserveDictionaryWhere`'s reads ran 0.91x to 1.01x and allocated the same; the lowest is a 7.0.0 arm with 1.3 ns of error.

### Changes to a view

No class in these runs carries both the 7.0.0 job and a view change. The figures of record remain those in `2026-09-28-the-waits-under-the-locks.md`, `BenchmarkRun-20260928-151833` against the 6.1.1 run `BenchmarkRun-20260927-185141`, whose query code is 7.0.0's: 0.97x to 1.03x for filtered and sorted views and 1.03x to 1.04x for grouped ones. Nothing since that run changes how a view follows a property change.

## What a view holds

From the footprint report at 20:29, per element at a thousand, above what the caller holds anyway: the collection, and for DynamicData's cache rows the cache as well, which itself holds 54.8 B per element.

| view | this library | DynamicData | NMF Expressions | ObservableComputations |
|---|---:|---:|---:|---:|
| filtered | 927.7 B | 1,865.0 B (list), 1,974.8 B (cache) | 1,281.1 B | **884.5 B** |
| sorted | 1,270.2 B | 1,921.9 B (cache) | 1,192.9 B | **897.6 B** |
| grouped, sixteen ways | 1,041.6 B | 2,001.7 B (cache) | 1,297.7 B | **859.5 B** |

At ten thousand a filtered view holds 9.11 MB here, 8.96 MB in ObservableComputations, 12.73 MB in NMF Expressions and 18.64 MB in DynamicData's list. **ObservableComputations holds the least in every shape at every size measured.** Five build-and-drop cycles leave 160 B here, 128 to 160 B in NMF Expressions and 320 B in ObservableComputations at every size but one: ObservableComputations' row at a thousand read −1,028,656 B, the harness's, since the inline row before it held about a megabyte its own frame kept alive. DynamicData's list leaves 103 to 294 KB that does not grow with the cycles.

**The copies:** observing a collection with nothing built over it holds 8.1 B per element at ten thousand, 8.2 at four thousand and 8.6 at a thousand, a reference each and the list around them; observing a dictionary holds 28.4 B per key at ten thousand. The release note had said the copy costs a reference per element without having measured it; it does.

## Against other libraries, an element moving in or out of a filtered view

`MembershipChangeComparison`, per property change above the unobserved floor, where each change negates a rank and so moves the element out of the view or back in:

| | this library | with a handler on the view | DynamicData, cache | NMF Expressions | ObservableComputations |
|---|---:|---:|---:|---:|---:|
| a thousand | **76.5 ns / 0 B** | 108.8 ns / 72 B | 306.7 ns / 704 B | 146.3 ns / 592 B | 573.5 ns / 84 B |
| ten thousand | **100.1 ns / 0 B** | 135.7 ns / 72 B | 604.7 ns / 704 B | 200.3 ns / 592 B | 6,753.2 ns / 84 B |

This library's view raises nothing when nothing handles it, while the others raise regardless, so the readmes quote the handled arm as the like-for-like figure. ObservableComputations' cost grew 11.78x for ten times the elements.

## The readmes and the release note

- Both readmes' comparison table has the membership rows and three retention rows, filtered, sorted and grouped, for all four libraries, with ObservableComputations in bold on each; the paragraph on what a live view holds quotes all four at ten thousand, and the case for ObservableComputations adds memory. The copy's cost is stated as 8 B per element and a dictionary's as about 28 B per key.
- The release note's cost line now compares with 7.0.0 rather than 6.1.0, from these runs, and says observing a dictionary costs less per change than it did.

## What remains

- **What makes a synchronized query's change twice as expensive, decomposed.** The queue, the deferral and the drain are not separated, and reading the code has not predicted costs in this library. Taken up in `2026-09-29-what-the-others-knew.md`: three monitor pairs a change around the queue, removed, for 0.61x to 0.78x of 7.0.1's time.
- **What each lock held while building costs, and whether it must grow with them.** About 23 to 27 ns a build per lock beyond the first; a concatenation of eight holds fifteen.
- **Why ObservableComputations' views hold less**, filtered by 5%, sorted by 29% and grouped by 17%. Taken up in `2026-09-29-what-the-others-knew.md`: a filtered view here now holds less than ObservableComputations'; sorted and grouped ones still hold more.
- **View changes against 7.0.0 in one run.** The six comparison classes carry no 7.0.0 job. Taken up against 7.0.1 in `2026-09-29-what-the-others-knew.md`.
