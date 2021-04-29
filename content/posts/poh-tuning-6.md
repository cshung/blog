---
title: "POH Tuning (Part 6 - Varying the benchmark)"
date: 2021-03-08T10:57:05-08:00
draft: false
---

# Varying the benchmark
In the [last post](../poh-tuning-5/), we showed that in a particular scenario, allocating pinned objects on pinned object heap is a better choice from both the speed perspective and the heap size perspective. How about other scenarios?

In [part 4](../poh-tuning-4), we already discussed the criterion what is feasible to test under GCPerfSim, so we will simply generate all the possibilities here with this simple Python script

```py
for a in range(1, 100):
    if 1000 % a == 0:
        for b in range(1, a):
            if a % b == 0:
                if ((1000 - b) % (a - b) == 0):
                    pin_sohsi = 1000 // a
                    pin_sohpi = a // b
                    poh_sohsi = (1000 - b) // (a - b)
                    poh_pohar = b
                    print("  2gb_pin_%s_%s:" % (a, b));
                    print("    arguments:");
                    print("      tc: 6");
                    print("      tagb: 100");
                    print("      tlgb: 2");
                    print("      lohar: 0");
                    print("      pohar: 0");
                    print("      sohsr: 100-4000");
                    print("      pohsr: 100-4000");
                    print("      sohsi: %s" % pin_sohsi);
                    print("      lohsi: 0");
                    print("      pohsi: 0");
                    print("      sohpi: %s" % pin_sohpi);
                    print("      lohpi: 0");
                    print("      sohfi: 0");
                    print("      lohfi: 0");
                    print("      pohfi: 0");
                    print("      allocType: reference");
                    print("      testKind: time");
                    print("  2gb_poh_%s_%s:" % (a, b));
                    print("    arguments:");
                    print("      tc: 6");
                    print("      tagb: 100");
                    print("      tlgb: 2");
                    print("      lohar: 0");
                    print("      pohar: %s" % poh_pohar);
                    print("      sohsr: 100-4000");
                    print("      pohsr: 100-4000");
                    print("      sohsi: %s" % poh_sohsi);
                    print("      lohsi: 0");
                    print("      pohsi: 1");
                    print("      sohpi: 0");
                    print("      lohpi: 0");
                    print("      sohfi: 0");
                    print("      lohfi: 0");
                    print("      pohfi: 0");
                    print("      allocType: reference");
                    print("      testKind: time");
```

# Preprocessing the result
As usual, we can create a pandas data frame for all the runs. Note that we used the benchmark name to keep track of the scenario, now we can extract these information using these simple python commands:

```py
run_data_frame['p'] = run_data_frame.apply(lambda row: row['benchmark_name'].split('_')[1], axis=1)
run_data_frame['a'] = run_data_frame.apply(lambda row: int(row['benchmark_name'].split('_')[2]), axis=1)
run_data_frame['b'] = run_data_frame.apply(lambda row: int(row['benchmark_name'].split('_')[3]), axis=1)
```

Now we can display the data in a nice sorted order

```py
run_data_frame[["p","a","b","PctTimePausedInGC", "speed", "HeapSizeBeforeMB_Mean", "HeapSizeAfterMB_Mean"]].sort_values(["p","a","b"])
```

|p  |a |b |PctTimePausedInGC|speed     |HeapSizeBeforeMB_Mean|HeapSizeAfterMB_Mean|
|---|--|--|-----------------|----------|---------------------|--------------------|
|pin|2 |1 |41.026208        |13.866402 |2909.199133          |2909.070073         |
|pin|4 |1 |40.996801        |12.284033 |3463.542373          |3463.398389         |
|pin|4 |2 |49.933891        |17.241924 |3589.816502          |3589.782947         |
|pin|8 |4 |57.689693        |23.644679 |3562.012412          |3562.028666         |
|pin|10|1 |37.903009        |9.974110  |3581.997371          |3581.919437         |
|pin|10|5 |59.205905        |22.360968 |3183.753481          |3182.977681         |
|pin|20|10|80.345651        |64.567706 |3902.958089          |3902.755635         |
|pin|40|8 |85.275268        |63.498679 |4017.525417          |4017.429858         |
|pin|40|10|87.644163        |75.736131 |3991.189973          |3991.090571         |
|pin|40|20|91.544744        |120.112824|4125.414157          |4125.391582         |
|pin|50|25|92.621281        |136.764843|4152.331355          |4152.325399         |
|poh|2 |1 |51.951706        |3.781857  |2229.854581          |2262.198264         |
|poh|4 |1 |45.390104        |3.555535  |2335.833893          |2362.571388         |
|poh|4 |2 |47.362661        |3.416029  |2283.330958          |2369.388038         |
|poh|8 |4 |50.333994        |4.037242  |2383.460738          |2578.549332         |
|poh|10|1 |53.589727        |4.143718  |2653.550769          |2684.079599         |
|poh|10|5 |48.989765        |3.985653  |2392.374988          |2635.762941         |
|poh|20|10|55.155276        |5.408592  |2295.580731          |2796.314600         |
|poh|40|8 |45.019697        |14.362240 |2780.002428          |2975.107003         |
|poh|40|10|45.041205        |12.911266 |2740.182679          |3042.987936         |
|poh|40|20|56.201348        |9.650450  |1889.063288          |2694.295634         |
|poh|50|25|52.596479        |12.271813 |1908.234109          |2592.491218         |

```py
run_data_frame[["p","a","b","TotalNumberGCs", "CountIsGen0", "CountIsGen1", "CountIsBackground", "CountIsBlockingGen2"]].sort_values(["p","a","b"])
```

|p  |a |b |TotalNumberGCs|CountIsGen0|CountIsGen1|CountIsBackground|CountIsBlockingGen2|
|---|--|--|--------------|-----------|-----------|-----------------|-------------------|
|pin|2 |1 |293           |270        |21         |0                |2                  |
|pin|4 |1 |284           |242        |40         |0                |2                  |
|pin|4 |2 |283           |240        |41         |0                |2                  |
|pin|8 |4 |285           |220        |63         |0                |2                  |
|pin|10|1 |330           |247        |81         |0                |2                  |
|pin|10|5 |286           |215        |69         |0                |2                  |
|pin|20|10|458           |366        |89         |0                |3                  |
|pin|40|8 |552           |486        |61         |0                |5                  |
|pin|40|10|565           |500        |60         |0                |5                  |
|pin|40|20|601           |537        |57         |0                |7                  |
|pin|50|25|633           |567        |57         |0                |9                  |
|poh|2 |1 |4902          |4875       |25         |0                |2                  |
|poh|4 |1 |4002          |3952       |48         |0                |2                  |
|poh|4 |2 |4549          |4493       |54         |0                |2                  |
|poh|8 |4 |4450          |4349       |99         |0                |2                  |
|poh|10|1 |4417          |4296       |119        |0                |2                  |
|poh|10|5 |4277          |4155       |119        |0                |3                  |
|poh|20|10|4176          |3941       |231        |0                |4                  |
|poh|40|8 |488           |338        |146        |0                |4                  |
|poh|40|10|471           |327        |140        |0                |4                  |
|poh|40|20|2849          |2411       |429        |0                |9                  |
|poh|50|25|622           |415        |191        |0                |16                 |

# Observation
The obvious observation is that POH is superior to pinned handle in all cases above for both speed and heap size aspect. From a number of GCs perspective, the 4000+ GCs in the POH case stands out. But even in those cases, the speed is still superior overall. Those in those cases we have a relatively low surivial rate. (Remember a is the number of objects to surive per 1000 objects)