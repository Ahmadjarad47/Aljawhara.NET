# Local performance

The benchmark used a 1,001-product PostgreSQL fixture and sequential loopback HTTP. Each baseline row has 12 measured requests after two warmups; all returned 200. Body sizes are decoded response bytes. These samples characterize a local fixture, not production or high concurrency.

| Request | Rows | Bytes | p50 | p95 |
| --- | ---: | ---: | ---: | ---: |
| Unfiltered catalog | 1,001 | 294,106 | 3.84 ms | 4.85 ms |
| Sorted catalog, page of 20 | 20 | 6,265 | 6.16 ms | 14.59 ms |
| Product detail | 1 | 616 | 4.55 ms | 5.32 ms |
| Shipping settings | — | 696 | 2.58 ms | 3.16 ms |

The unfiltered catalog returns every product and used the repository's existing cache in this warm run. It transferred about 47 times as much JSON as the page of 20; its lower loopback time does not erase the payload cost. The paged path loads details and variants that its summary DTO never reads. The [representative root query plan](catalog-root-plan.txt) used a sequential scan and top-N sort and took 0.283 ms on this fixture, so it does not justify an index on its own.

The code now skips detail, variant and variant value collections only for filtered summary results. Ratings and categories remain loaded; the full detail path keeps its existing query shape. No cache was added. The instrumented request went from **five to three product-related SQL commands**. Baseline and optimized JSON matched for newest, category, rating sort, and discount sort on the same database. In the two runs of 20 samples per build, p50 changed from 8.05 to 7.66 ms and 6.34 to 5.66 ms. p95 changed from 11.55 to 16.91 ms and 6.91 to 6.74 ms. The work reduction is repeatable from the query shape; a tail latency improvement is not established. See [first comparison](catalog-comparison.json), [second comparison](catalog-comparison-repeat.json), and [command evidence](catalog-query-count.json).

The optimized Angular client build was 657.27 kB initial raw, 144.78 kB estimated transfer. Lazy chunks included jsPDF (410.17 kB raw), html2canvas (203.21 kB), and the main product route chunk (206.92 kB). Browser traffic repeated category and shipping reads across navigations; the trace does not establish a same-render duplicate or isolate its cost. The JWT pipeline includes a database session check on authenticated requests; no allocation profiler, pool saturation test, or browser main-thread profile was run. The two measured backend bottlenecks were excess split-query work and the full-catalog payload; bundle sizes and repeated reads are follow-up evidence.
