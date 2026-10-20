# Evaluation report

Run 2026-10-20 19:59 UTC against `http://localhost:5050/` — **ALL PASSED**

| Metric | Value |
|---|---|
| Model checks (deterministic, no LLM) | 9/9 |
| Agent cases (all checks pass) | 13/13 |
| Agent checks | 47/47 |
| Agent latency avg / p95 | 3.9 s / 5.9 s |
| Tokens | 65,415 |
| Models used | Groq/openai/gpt-oss-120b ×9, Groq/openai/gpt-oss-20b ×4 |
| LLM fallbacks during run | 5 |

## Model checks

| | Id | Expectation | Result |
|---|---|---|---|
| ✅ | M1 | Minneapolis is the Midwest hub most exposed to winter disruption | minneapolis = minneapolis |
| ✅ | M2 | Miami is more exposed to hurricanes than Houston | 98.6 > 87.6 |
| ✅ | M3 | Houston is more exposed to flooding than Miami | 81.4 > 56.4 |
| ✅ | M4 | Denver had snow on a plausible share of days last year (1–30%) | 4.66 in [1, 30] |
| ✅ | M5 | Dallas is rated High or Very High overall (the brief calls it high) | High one of [High, VeryHigh] |
| ✅ | M6 | Phoenix is the hub most exposed to extreme heat | phoenix = phoenix |
| ✅ | M7 | Hurricanes don't apply to Denver (FEMA not applicable → 0) | 0 = 0 |
| ✅ | M8 | Seattle is among the three least exposed hubs overall | 15 >= 13 |
| ✅ | M9 | Every hub is scored with High data confidence (complete weather + FEMA data) | all 15 values = High |

## Agent cases

| | Id | Question | Tools | Model | Latency | Checks |
|---|---|---|---|---|---|---|
| ✅ | A1 | Which hubs in the Midwest are most exposed to winter disruption? | rank_hubs | Groq/openai/gpt-oss-120b | 3.8 s | 4/4 |
| ✅ | A2 | Compare Miami and Houston in terms of hurricane and flood exposure. | compare_hubs | Groq/openai/gpt-oss-120b | 3.6 s | 6/6 |
| ✅ | A3 | What percentage of days in Denver last year had snowfall? | get_weather_statistics | Groq/openai/gpt-oss-120b | 2.0 s | 4/4 |
| ✅ | A4 | Why is the Dallas hub's weather disruption risk high? | get_hub_risk | Groq/openai/gpt-oss-120b | 4.2 s | 5/5 |
| ✅ | A5 | How does it compare with Houston on those drivers? | compare_hubs | Groq/openai/gpt-oss-120b | 3.7 s | 3/3 |
| ✅ | A6 | Why is the Seattle hub's weather risk so high? | get_hub_risk | Groq/openai/gpt-oss-120b | 4.6 s | 4/4 |
| ✅ | A7 | What's the capital of France? |  | Groq/openai/gpt-oss-20b | 4.8 s | 2/2 |
| ✅ | A8 | How is the risk score calculated? | describe_risk_model | Groq/openai/gpt-oss-20b | 3.5 s | 4/4 |
| ✅ | A9 | Which three hubs should we prioritize for resilience investment? | rank_hubs | Groq/openai/gpt-oss-120b | 2.6 s | 4/4 |
| ✅ | A10 | What is the flood risk of the Boston hub? | list_hubs | Groq/openai/gpt-oss-20b | 3.6 s | 2/2 |
| ✅ | A11 | Which hub is most exposed to extreme heat? | rank_hubs | Groq/openai/gpt-oss-120b | 2.9 s | 3/3 |
| ✅ | A12 | Is the Denver hub exposed to hurricanes? | get_hub_risk | Groq/openai/gpt-oss-20b | 5.9 s | 3/3 |
| ✅ | A13 | How many heavy-rain days did the Houston hub have in 2024? | get_weather_statistics | Groq/openai/gpt-oss-120b | 5.1 s | 3/3 |

## Details

### ✅ A1 — Which hubs in the Midwest are most exposed to winter disruption?

*Purpose:* Brief question 1: regional hazard ranking

- ✅ `intent` — Ranking
- ✅ `toolCalled(rank_hubs)` — args ⊇ { "region": "Midwest", "hazard": "Winter" }
- ✅ `mentionsTopRanked(region=Midwest&hazard=Winter, 3)` — mentions Minneapolis, Chicago, Detroit
- ✅ `grounded` — 5 findings, all grounded = True

<details><summary>Answer</summary>

### Midwest Winter‑Disruption Exposure (2021‑2025)
| Rank | Hub | Winter Score | Rating | Top Drivers |
|------|-----|--------------|--------|-------------|
| 1 | **Minneapolis** | **70.8** | High | Winter, SevereStorm |
| 2 | **Chicago** | **56.9** | High | SevereStorm, Winter |
| 3 | **Detroit** | **54.1** | High | Winter, SevereStorm |
| 4 | **Kansas City** | **43.8** | Moderate | Flood, SevereStorm |
| 5 | **Indianapolis** | **40.0** | Moderate | Flood, Winter |

The ranking is driven by the Winter hazard score. Minneapolis leads with a 70.8 score, reflecting frequent snow/ice events and high FEMA‑baseline percentiles. Chicago and Detroit follow, with strong contributions from both Winter and SevereStorm signals. Kansas City and Indianapolis rank lower, their Winter scores moderated by other hazards (Flood, SevereStorm). All scores have **High confidence** based on the 2021‑2025 observation window.


</details>

### ✅ A2 — Compare Miami and Houston in terms of hurricane and flood exposure.

*Purpose:* Brief question 2: two-hub, two-hazard comparison

- ✅ `intent` — Comparison
- ✅ `toolCalled(compare_hubs)` — args ⊇ { "hubIds": [ "miami", "houston" ] }
- ✅ `mentionsHubs` — mentions Miami, Houston
- ✅ `citesApiValue(rows[metric=Hurricane].values[hubId=miami].score)` — cites 98.6
- ✅ `citesApiValue(rows[metric=Flood].values[hubId=houston].score)` — cites 81.4
- ✅ `grounded` — 6 findings, all grounded = True

<details><summary>Answer</summary>

**Hurricane exposure** – Miami scores **98.6 (VeryHigh)**, outpacing Houston’s **87.6 (VeryHigh)**. The gap (≈11 points) is driven by Miami’s higher *nriHurricane* percentile (98.6 vs 87.6) despite identical observed gust‑day counts (0.2 days/yr).\
**Flood exposure** – Houston is considerably higher at **81.4 (VeryHigh)** versus Miami’s **56.4 (High)** (≈25‑point spread). Houston’s flood risk is amplified by more heavy‑rain days (4.2 vs 2.2 days/yr) and higher riverine (98.4 vs 96.5 percentile) and coastal flooding percentiles (87.3 vs 55.7).\
Overall, Miami’s composite risk (70.9) is slightly above Houston’s (69.3) because its hurricane driver outweighs Houston’s stronger flood driver. Both hubs carry **High** composite ratings, with **High** confidence in the scores.

</details>

### ✅ A3 — What percentage of days in Denver last year had snowfall?

*Purpose:* Brief question 3: weather statistic for 'last year'

- ✅ `intent` — WeatherStatistics
- ✅ `toolCalled(get_weather_statistics)` — args ⊇ { "hubId": "denver" }
- ✅ `citesApiValue(disruptionDays[metric=snowDays].percentOfDays)` — cites 4.66
- ✅ `grounded` — 1 findings, all grounded = True

<details><summary>Answer</summary>

In 2025 Denver experienced snowfall on **4.66 %** of days (17 snow days out of 365).

</details>

### ✅ A4 — Why is the Dallas hub's weather disruption risk high?

*Purpose:* Brief question 4: explanation from drivers and evidence

- ✅ `intent` — HubRisk
- ✅ `toolCalled(get_hub_risk)` — args ⊇ { "hubId": "dallas" }
- ✅ `mentionsApiText(topDrivers[0])` — mentions Flood
- ✅ `citesApiValue(compositeScore)` — cites 64.6
- ✅ `grounded` — 7 findings, all grounded = True

<details><summary>Answer</summary>

Dallas scores **64.6** (High) overall, driven primarily by **Flood** (score 78.9, contribution 47.34) and **Severe Storm** (score 63.3, contribution 6.33). The flood driver reflects a very high river‑ine flood frequency (97.79 percentile) and 3.6 days/yr of heavy rain (>50 mm). Severe‑storm risk is amplified by 2.8 days/yr of damaging gusts and extreme tornado, hail and strong‑wind percentiles (≈96 percentile). Together these two hazards account for over 80 % of the composite score, pushing Dallas into the High rating despite modest winter and hurricane exposure. Heat and hurricane risks are moderate and add only small increments.

</details>

### ✅ A5 — How does it compare with Houston on those drivers?

*Purpose:* Follow-up: 'it' and 'those drivers' must resolve from conversation memory

- ✅ `toolCalled(compare_hubs)` — args ⊇ { "hubIds": [ "dallas", "houston" ] }
- ✅ `mentionsHubs` — mentions Dallas, Houston
- ✅ `grounded` — 8 findings, all grounded = True

<details><summary>Answer</summary>

Both Dallas and Houston rank **High** (Dallas 64.6, Houston 69.3). Dallas’s risk is driven by **Flood** (78.9) and **Severe‑Storm** (63.3) scores, reflecting a 97.8 percentile riverine‑flood frequency and 2.8 days/yr of damaging gusts. Houston’s risk is dominated by **Hurricane** (87.6) and **Flood** (81.4) scores, driven by an 87.6 percentile hurricane exposure and 98.4 percentile riverine‑flood frequency (plus coastal flooding). Together these hazards account for >80 % of each hub’s composite score, explaining why both are high‑risk but for different primary drivers.

</details>

### ✅ A6 — Why is the Seattle hub's weather risk so high?

*Purpose:* False premise: Seattle is the least exposed hub — the agent should say so, not agree

- ✅ `intent` — HubRisk
- ✅ `toolCalled(get_hub_risk)` — args ⊇ { "hubId": "seattle" }
- ✅ `mentionsApiText(rating)` — mentions Low
- ✅ `grounded` — 5 findings, all grounded = True

<details><summary>Answer</summary>

Seattle’s overall weather‑risk rating is **Low** (composite score 23.6/100). The model flags **Winter** and **Flood** as the top drivers, but even these hazards remain modest. Winter scores 29.7 (moderate) driven by 8.6 snow days / yr and a very high FEMA Winter‑weather percentile (95.87), yet the observed winter score (12.9) is far below the baseline (54.9), pulling the contribution down to 18.6 points. Flood risk is low (score 19.4) with only 0.2 heavy‑rain days / yr and riverine flood percentile 11.0. Severe‑storm and Heat scores are also low, each contributing ~1–2 points. Consequently, Seattle’s composite exposure stays well under the “High” threshold.

</details>

### ✅ A7 — What's the capital of France?

*Purpose:* Out of scope: decline politely without calling tools

- ✅ `intent` — OutOfScope
- ✅ `noToolCalls` — no tools called

<details><summary>Answer</summary>

I’m sorry, but I can’t help with that. I can assist with weather‑risk analysis for your distribution hubs.

</details>

### ✅ A8 — How is the risk score calculated?

*Purpose:* Methodology: explain the deterministic model from the model-description tool

- ✅ `intent` — Methodology
- ✅ `toolCalled(describe_risk_model)` — describe_risk_model
- ✅ `mentionsText` — found 'FEMA'
- ✅ `mentionsText` — found 'worst'

<details><summary>Answer</summary>

The composite score is a deterministic, rule‑based index that blends two signals for each hazard:
1. **Observed disruption days** – daily weather data from Open‑Meteo over 2021‑2025 (e.g., snow days, hard‑freeze days, high‑gust days, heavy rain, extreme heat). Each hazard’s observed component is scaled to 0‑100 based on the hub’s own annual averages.
2. **FEMA National Risk Index (NRI)** – the county‑level annualized hazard frequency percentile (e.g., Winter Weather, Hurricane, Flood, Severe Storm, Heat Wave). If FEMA data is not applicable for a hazard, that component is 0.

For each hazard, the final hazard score = 0.5 × worst‑hazard blend + 0.5 × weighted average of the other hazards, where the worst hazard is the one with the highest observed or FEMA signal. The composite is then 50 % of the worst hazard score plus 50 % of the weighted average of all hazards, ensuring an extreme event dominates the overall exposure.

Ratings are mapped to score bands: Low < 25, Moderate < 50, High < 75, VeryHigh ≥ 75.

**Key assumptions**: weights, thresholds, and caps are expert‑defined and not calibrated to actual disruption data; the index ranks relative exposure, not absolute closure risk.

**Uncertainty**: data covers 2021‑2025; county‑level FEMA data may not match the exact hub location; weights are uncalibrated.


</details>

### ✅ A9 — Which three hubs should we prioritize for resilience investment?

*Purpose:* Overall prioritization: composite ranking, top 3

- ✅ `intent` — Ranking
- ✅ `toolCalled(rank_hubs)` — rank_hubs
- ✅ `mentionsTopRanked(top=3, 3)` — mentions New Orleans, Miami, Houston
- ✅ `grounded` — 3 findings, all grounded = True

<details><summary>Answer</summary>

Based on the composite weather‑disruption exposure scores (2021‑2025), the three hubs with the highest risk are:
1. **New Orleans, LA** – Composite score **71.2** (High). Top drivers: Hurricane and Flood.
2. **Miami, FL** – Composite score **70.9** (High). Top drivers: Hurricane and Flood.
3. **Houston, TX** – Composite score **69.3** (High). Top drivers: Hurricane and Flood.
All three score in the High band and are driven primarily by extreme hurricane activity and flood risk, which dominate their exposure profiles.

</details>

### ✅ A10 — What is the flood risk of the Boston hub?

*Purpose:* Unknown hub: must not invent data for a hub that doesn't exist

- ✅ `mentionsText` — found 'not in'
- ✅ `grounded` — 0 findings, all grounded = True

<details><summary>Answer</summary>

The Boston hub is not in our current list of distribution hubs, so we cannot provide a flood risk score for it. If you need flood risk for one of the listed hubs (e.g., New York or any other), let me know and I’ll retrieve the data.

</details>

### ✅ A11 — Which hub is most exposed to extreme heat?

*Purpose:* Single-hazard ranking across all regions

- ✅ `toolCalled(rank_hubs)` — args ⊇ { "hazard": "Heat" }
- ✅ `mentionsTopRanked(hazard=Heat, 1)` — mentions Phoenix
- ✅ `grounded` — 1 findings, all grounded = True

<details><summary>Answer</summary>

**Phoenix Hub** is the most exposed to extreme heat, with a Heat‑specific score of **99** (VeryHigh rating). The heat driver dominates its risk profile, and the model assigns a high confidence to this assessment.

</details>

### ✅ A12 — Is the Denver hub exposed to hurricanes?

*Purpose:* Not-applicable hazard: should explain hurricanes don't apply (score 0)

- ✅ `toolCalled(get_hub_risk)` — args ⊇ { "hubId": "denver" }
- ✅ `mentionsText` — found 'not applicable'
- ✅ `grounded` — 2 findings, all grounded = True

<details><summary>Answer</summary>

Denver’s hurricane exposure is negligible. The composite hurricane score is **0** (rating Low), with no contribution to the overall risk. The only observed signal is 1 day per year of tropical‑storm‑force gusts, and FEMA’s National Risk Index lists the hurricane hazard as not applicable for Denver’s county.

**Key drivers:**
- Hurricane score 0, baseline 0, contribution 0.
- 1 day/yr of tropical‑storm gusts.
- FEMA NRI marks hurricane as not applicable.

Thus, Denver is not exposed to hurricanes and should not be prioritized for hurricane‑specific resilience.

</details>

### ✅ A13 — How many heavy-rain days did the Houston hub have in 2024?

*Purpose:* Explicit period: tool arguments must carry the date range; count must match

- ✅ `toolCalled(get_weather_statistics)` — args ⊇ { "hubId": "houston", "from": "2024-01-01", "to": "2024-12-31" }
- ✅ `citesApiValue(disruptionDays[metric=heavyRainDays].days)` — cites 4
- ✅ `grounded` — 1 findings, all grounded = True

<details><summary>Answer</summary>

In 2024, the Houston hub experienced **4 heavy‑rain days** (precipitation ≥ 50 mm).

</details>

