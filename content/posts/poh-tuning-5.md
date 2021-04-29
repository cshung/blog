---
title: "POH Tuning (Part 5 - Preliminary results)"
date: 2021-03-08T10:57:05-08:00
draft: false
---
# Top level result
After running, we can use the Jupyter notebook to analyze the result. The result is surprising. To make the data easy to analyze, they are available as [pandas](https://pandas.pydata.org/) data frame. For those who are unfamiliar with pandas, a data frame is really just a table.

To use the notebook to get to the data frame, we need to run the first cell (as it required to setup the functions), and then we can run the cell calling the `get_test_metrics_numbers_for_jupyter` function, there should be exactly one such cell. And this cell will give us the `run_data_frame` variable.

With that, running this command is going to give us the key metrics.

```py
run_data_frame[["benchmark_name", "PctTimePausedInGC", "speed", "HeapSizeBeforeMB_Mean", "HeapSizeAfterMB_Mean"]]
```

|benchmark_name|PctTimePausedInGC|speed    |HeapSizeBeforeMB_Mean|HeapSizeAfterMB_Mean|
|--------------|-----------------|---------|---------------------|--------------------|
|2gb_pinning   |83.886439        |56.585445|4009.213205          |4009.053025         |
|2gb_poh       |60.832619        |27.530971|2883.491157          |2947.730790         |

At a glance, the speed is significantly reduced despite all other metrics shows improvement. This is just weird. 

# The weird speed metric
Upon further investigation, I figured out that the `speed` metric reported by the infrastructure is really just the geometric mean of `FirstToLastGCSeconds` and `PauseDurationMSec_95P`. Both of them are measured in seconds, so the value is the smaller the better. So in fact, we are showing an improvement in the timing aspect as well.

# What caused the improvement?
Apparently we are spending much less time in GC. Let's take a look at the number of GCs first.

```py
run_data_frame[["benchmark_name", "TotalNumberGCs", "CountIsGen0", "CountIsGen1", "CountIsBackground", "CountIsBlockingGen2"]]
```

|benchmark_name|TotalNumberGCs|CountIsGen0|CountIsGen1|CountIsBackground|CountIsBlockingGen2|
|--------------|--------------|-----------|-----------|-----------------|-------------------|
|2gb_pinning   |683           |557        |117        |0                |9                  |
|2gb_poh       |435           |278        |149        |0                |8                  |

Looking at the count data, we realize that there is much less gen 0 GCs. Sadly, while the data tell us there is reduced number of gen 0 GC, we do not understand why from the data. In a future post, I am going to talk about the how to analyze why do we have a significantly reduced number of GCs.

# Are we just observing an effect from chance?
As with any scientific studies, it is important to note that a single experiment result doesn't mean much. If it could not be reproduced or it could have occurred by chance, then it isn't particularly meaningful. To that end, I did an experiment and run the pair of benchmarks multiple times.

Here are the top level metrics for 5 pairs:

|benchmark_name|PctTimePausedInGC|speed    |HeapSizeBeforeMB_Mean|HeapSizeAfterMB_Mean|
|--------------|-----------------|---------|---------------------|--------------------|
|2gb_pinning   |84.152044        |55.317034|4018.010300          |4017.822676         |
|2gb_pinning   |84.332101        |54.444867|4021.135100          |4021.038674         |
|2gb_pinning   |84.561195        |56.717987|4024.397596          |4024.306514         |
|2gb_pinning   |84.601454        |52.776987|4006.901215          |4006.789502         |
|2gb_pinning   |84.410538        |53.956592|4006.952758          |4006.901196         |
|2gb_poh       |61.779599        |26.968380|2926.503793          |2960.753005         |
|2gb_poh       |60.978760        |26.036596|2890.863907          |2940.875974         |
|2gb_poh       |60.329187        |22.559079|2900.956131          |2956.372021         |
|2gb_poh       |61.475650        |26.602588|2890.614196          |2941.809857         |
|2gb_poh       |60.654861        |22.511328|2876.809548          |2932.633737         |

and here are the GC counts

|benchmark_name|TotalNumberGCs|CountIsGen0|CountIsGen1|CountIsBackground|CountIsBlockingGen2|
|--------------|--------------|-----------|-----------|-----------------|-------------------|
|2gb_pinning   |595           |491        |95         |0                |9                  |
|2gb_pinning   |611           |501        |101        |0                |9                  |
|2gb_pinning   |614           |510        |95         |0                |9                  |
|2gb_pinning   |611           |502        |100        |0                |9                  |
|2gb_pinning   |611           |504        |98         |0                |9                  |
|2gb_poh       |390           |243        |139        |0                |8                  |
|2gb_poh       |412           |259        |145        |0                |8                  |
|2gb_poh       |480           |305        |167        |0                |8                  |
|2gb_poh       |414           |259        |147        |0                |8                  |
|2gb_poh       |484           |310        |166        |0                |8                  |

In this case, the data is fairly obvious, the observation we saw is fairly stable. To make this even more concrete, we could use the [Students' T test](https://en.wikipedia.org/wiki/Student%27s_t-test). Here I show that the decrease of number of gen 0 GC is statistically significant. To perform the test, we run these (it is not currently in the Jupyter notebook, but it could be)

```py
from scipy.stats import ttest_ind

cat1 = run_data_frame[run_data_frame['benchmark_name']=='2gb_pinning']
cat2 = run_data_frame[run_data_frame['benchmark_name']=='2gb_poh']

ttest_ind(cat1['CountIsGen0'], cat2['CountIsGen0'], equal_var=False)
```

The code produced this result:

```
Ttest_indResult(statistic=16.317779324341963, pvalue=4.106432582405551e-05)
```

The statistics is just a number used in the calculation. The key is a small p-value. Basically, the p-value is the probability of observing the experimental result if we assume the number of gen0 collection in both cases is equal. Since the probability is so small, we choose to believe otherwise, that the number of gen0 collections is indeed different.

For those who are familiar with statistics, here are some fine details for the test. We used unequal variance because we see and obvious flucation in the gen 0 counts in the pinned handle case but not in the pinned object heap case. The p-value outputted by scipy is a two-tailed p-value. Since we wanted to prove that the number of gen 0 is indeed smaller, we should have used a one-tailed p-value instead, which should be the number divided by 2, but they are small anyway.
