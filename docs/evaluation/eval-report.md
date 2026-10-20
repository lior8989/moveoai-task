# Evaluation report

Run 2026-10-20 20:25 UTC against `http://localhost:5050/` — **ALL PASSED**

| Metric | Value |
|---|---|
| Model checks (deterministic, no LLM) | 9/9 |
| Agent cases (all checks pass) | 13/13 |
| Agent checks | 47/47 |
| Agent latency avg / p95 | 3.3 s / 6.5 s |
| Tokens | 66,870 |
| Models used | Groq/openai/gpt-oss-120b ×12, Groq/openai/gpt-oss-20b ×1 |
| LLM fallbacks during run | 2 |

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
| ✅ | A1 | Which hubs in the Midwest are most exposed to winter disruption? | rank_hubs | Groq/openai/gpt-oss-120b | 3.5 s | 4/4 |
| ✅ | A2 | Compare Miami and Houston in terms of hurricane and flood exposure. | compare_hubs | Groq/openai/gpt-oss-120b | 4.3 s | 6/6 |
| ✅ | A3 | What percentage of days in Denver last year had snowfall? | get_weather_statistics | Groq/openai/gpt-oss-120b | 2.3 s | 4/4 |
| ✅ | A4 | Why is the Dallas hub's weather disruption risk high? | get_hub_risk | Groq/openai/gpt-oss-120b | 3.0 s | 5/5 |
| ✅ | A5 | How does it compare with Houston on those drivers? | compare_hubs | Groq/openai/gpt-oss-120b | 4.0 s | 3/3 |
| ✅ | A6 | Why is the Seattle hub's weather risk so high? | get_hub_risk | Groq/openai/gpt-oss-120b | 5.0 s | 4/4 |
| ✅ | A7 | What's the capital of France? |  | Groq/openai/gpt-oss-20b | 6.5 s | 2/2 |
| ✅ | A8 | How is the risk score calculated? | describe_risk_model | Groq/openai/gpt-oss-120b | 2.6 s | 4/4 |
| ✅ | A9 | Which three hubs should we prioritize for resilience investment? | rank_hubs | Groq/openai/gpt-oss-120b | 2.4 s | 4/4 |
| ✅ | A10 | What is the flood risk of the Boston hub? | list_hubs | Groq/openai/gpt-oss-120b | 2.0 s | 2/2 |
| ✅ | A11 | Which hub is most exposed to extreme heat? | rank_hubs | Groq/openai/gpt-oss-120b | 1.9 s | 3/3 |
| ✅ | A12 | Is the Denver hub exposed to hurricanes? | get_hub_risk | Groq/openai/gpt-oss-120b | 2.4 s | 3/3 |
| ✅ | A13 | How many heavy-rain days did the Houston hub have in 2024? | get_weather_statistics | Groq/openai/gpt-oss-120b | 2.8 s | 3/3 |

## Details

### ✅ A1 — Which hubs in the Midwest are most exposed to winter disruption?

*Purpose:* Brief question 1: regional hazard ranking

- ✅ `intent` — Ranking
- ✅ `toolCalled(rank_hubs)` — args ⊇ { "region": "Midwest", "hazard": "Winter" }
- ✅ `mentionsTopRanked(region=Midwest&hazard=Winter, 3)` — mentions Minneapolis, Chicago, Detroit
- ✅ `grounded` — 5 findings, all grounded = True

<details><summary>Answer</summary>

### Midwest Winter‑Disruption Exposure (2021‑2025)
| Rank | Hub | Winter Score | Rating |
|------|----------------|--------------|--------|
| 1 | **Minneapolis** | **70.8** | High |
| 2 | **Chicago** | **56.9** | High |
| 3 | **Detroit** | **54.1** | High |
| 4 | **Kansas City** | **43.8** | Moderate |
| 5 | **Indianapolis** | **40.0** | Moderate |

**Why these rankings?** The Winter hazard dominates the top three hubs (Minneapolis, Chicago, Detroit), each flagged as a top driver alongside SevereStorm. Kansas City and Indianapolis rank lower because Flood and other hazards dilute their Winter exposure. All scores derive from observed 2021‑2025 weather and FEMA baseline percentiles, with high model confidence.

**Key take‑away:** Prioritize resilience investments for Minneapolis, Chicago, and Detroit to mitigate winter‑related disruptions.

</details>

### ✅ A2 — Compare Miami and Houston in terms of hurricane and flood exposure.

*Purpose:* Brief question 2: two-hub, two-hazard comparison

- ✅ `intent` — Comparison
- ✅ `toolCalled(compare_hubs)` — args ⊇ { "hubIds": [ "miami", "houston" ] }
- ✅ `mentionsHubs` — mentions Miami, Houston
- ✅ `citesApiValue(rows[metric=Hurricane].values[hubId=miami].score)` — cites 98.6
- ✅ `citesApiValue(rows[metric=Flood].values[hubId=houston].score)` — cites 81.4
- ✅ `grounded` — 4 findings, all grounded = True

<details><summary>Answer</summary>

**Hurricane exposure** – Miami scores **98.6** (VeryHigh) versus Houston’s **87.6** (VeryHigh). Both hubs rank at the top of the hurricane driver, driven by high *nriHurricane* percentiles (Miami 98.6th, Houston 87.6th) and similar tropical‑storm gust days (0.2 days/yr).

**Flood exposure** – Houston scores **81.4** (VeryHigh) while Miami scores **56.4** (High). Houston’s flood risk is amplified by more heavy‑rain days (4.2 days/yr vs 2.2 days/yr) and higher riverine and coastal flood percentiles (Riverine 98.4th, Coastal 87.3rd) compared with Miami (Riverine 96.5th, Coastal 55.7th).

Overall, Miami is more vulnerable to hurricanes, whereas Houston faces a stronger flood threat.

</details>

### ✅ A3 — What percentage of days in Denver last year had snowfall?

*Purpose:* Brief question 3: weather statistic for 'last year'

- ✅ `intent` — WeatherStatistics
- ✅ `toolCalled(get_weather_statistics)` — args ⊇ { "hubId": "denver" }
- ✅ `citesApiValue(disruptionDays[metric=snowDays].percentOfDays)` — cites 4.66
- ✅ `grounded` — 1 findings, all grounded = True

<details><summary>Answer</summary>

In 2025 Denver experienced snowfall on **4.66 % of days** (17 snow days out of 365).

</details>

### ✅ A4 — Why is the Dallas hub's weather disruption risk high?

*Purpose:* Brief question 4: explanation from drivers and evidence

- ✅ `intent` — HubRisk
- ✅ `toolCalled(get_hub_risk)` — args ⊇ { "hubId": "dallas" }
- ✅ `mentionsApiText(topDrivers[0])` — mentions Flood
- ✅ `citesApiValue(compositeScore)` — cites 64.6
- ✅ `grounded` — 3 findings, all grounded = True

<details><summary>Answer</summary>

Dallas scores **64.6** (High) overall, driven primarily by **Flood** (score 78.9, VeryHigh) and **Severe Storm** (score 63.3, High). Flood contributes **47.34** points – the largest share – because Dallas experiences frequent heavy‑rain days (3.6 days/yr) and sits in the 97.8‑th percentile for riverine‑flood risk (FEMA NRI). Severe‑storm risk adds **6.33** points, reflecting 2.8 days/yr of damaging gusts and very high tornado (96.4 th) and hail (96.9 th) percentiles. Heat also lifts the score (+4.81) with 19 days/yr of extreme heat and a 96.8 th percentile heat‑wave risk. Winter and hurricane hazards are low, so they do not offset the high flood‑driven exposure. Together these drivers push Dallas into the High rating.

</details>

### ✅ A5 — How does it compare with Houston on those drivers?

*Purpose:* Follow-up: 'it' and 'those drivers' must resolve from conversation memory

- ✅ `toolCalled(compare_hubs)` — args ⊇ { "hubIds": [ "dallas", "houston" ] }
- ✅ `mentionsHubs` — mentions Dallas, Houston
- ✅ `grounded` — 10 findings, all grounded = True

<details><summary>Answer</summary>

**Overall exposure** – Dallas (64.6) and Houston (69.3) are both rated *High*, with Houston slightly higher overall. 

**Flood** – Houston leads (81.4 vs 78.9) driven by more heavy‑rain days (4.2 vs 3.6 days/yr) and a higher FEMA riverine‑flood percentile (98.4 th vs 97.8 th). 

**Hurricane** – Houston’s score jumps to *VeryHigh* (87.6) because its hurricane percentile (87.6 th) far exceeds Dallas (34.3 th). 

**Severe Storm** – Dallas dominates (63.3 vs 36.2) thanks to many damaging‑gust days (2.8 vs 0.8 days/yr) and very high tornado/hail percentiles (≈96 th). 

**Heat** – Dallas also scores higher (64.1 vs 43.4) with more extreme‑heat days (19 vs 5.2 days/yr) and a higher heat‑wave percentile (96.8 th vs 91.1 th). 

**Key takeaway** – Both hubs are high‑risk, but the *driver mix* differs: Houston’s risk is driven by hurricane and flood exposure, while Dallas’s risk is driven by severe‑storm and heat exposure.

</details>

### ✅ A6 — Why is the Seattle hub's weather risk so high?

*Purpose:* False premise: Seattle is the least exposed hub — the agent should say so, not agree

- ✅ `intent` — HubRisk
- ✅ `toolCalled(get_hub_risk)` — args ⊇ { "hubId": "seattle" }
- ✅ `mentionsApiText(rating)` — mentions Low
- ✅ `grounded` — 5 findings, all grounded = True

<details><summary>Answer</summary>

The Seattle hub’s overall weather‑risk rating is **Low** (composite score 23.6/100). The model flags **Winter** as the primary driver (score 29.7, Moderate) because Seattle experiences about **8.6 snow days / yr** and ranks in the **95.9 percentile** for winter‑weather frequency on the FEMA National Risk Index. Flood risk is the second driver (score 19.4, Low) with **0.2 heavy‑rain days / yr** and a **60 percentile** for coastal flooding. Severe‑storm exposure also adds modestly (score 18, Low) driven by **4 damaging‑gust days / yr**. Heat and hurricane hazards are negligible. Together these contributions keep the composite score low despite the high winter‑weather percentile.

</details>

### ✅ A7 — What's the capital of France?

*Purpose:* Out of scope: decline politely without calling tools

- ✅ `intent` — OutOfScope
- ✅ `noToolCalls` — no tools called

<details><summary>Answer</summary>

I’m sorry, but I can’t answer that. I can help with weather‑risk analysis for your distribution hubs.

</details>

### ✅ A8 — How is the risk score calculated?

*Purpose:* Methodology: explain the deterministic model from the model-description tool

- ✅ `intent` — Methodology
- ✅ `toolCalled(describe_risk_model)` — describe_risk_model
- ✅ `mentionsText` — found 'FEMA'
- ✅ `mentionsText` — found 'worst'

<details><summary>Answer</summary>

The Weather‑Risk Index is a deterministic, rule‑based composite score (0‑100). For each of the five hazards – Winter, Hurricane, Flood, Severe Storm and Heat – a hazard‑specific score is built by blending two signals:
1. **Observed disruption days** at the hub (Open‑Meteo daily reanalysis, 2021‑2025) – e.g., % of days with snow, hard‑freeze, heavy rain, high gusts, extreme heat. Each signal is weighted (observedWeight) and capped at 100.
2. **FEMA National Risk Index (NRI) percentile** for the county, representing long‑term hazard frequency.
The hazard score = (observedWeight × observedSignal + (1‑observedWeight) × baselineSignal) × compositeWeight. 
The **composite risk score** combines the weighted average of all hazard scores (50 % of total) with the worst‑hazard score (50 % blend), ensuring a single extreme hazard is not diluted. Ratings are Low (<25), Moderate (25‑<50), High (50‑<75), VeryHigh (≥75). All weights, thresholds and caps are expert assumptions, not calibrated to actual disruption outcomes.


</details>

### ✅ A9 — Which three hubs should we prioritize for resilience investment?

*Purpose:* Overall prioritization: composite ranking, top 3

- ✅ `intent` — Ranking
- ✅ `toolCalled(rank_hubs)` — rank_hubs
- ✅ `mentionsTopRanked(top=3, 3)` — mentions New Orleans, Miami, Houston
- ✅ `grounded` — 3 findings, all grounded = True

<details><summary>Answer</summary>

Based on the composite weather‑disruption exposure scores (2021‑2025), the three hubs with the highest risk are:
1. **New Orleans, LA** – Score 71.2 (High) – driven mainly by Hurricane and Flood exposure.
2. **Miami, FL** – Score 70.9 (High) – driven mainly by Hurricane and Flood exposure.
3. **Houston, TX** – Score 69.3 (High) – driven mainly by Hurricane and Flood exposure.
All three have “High” ratings and the model flags Hurricane and Flood as their top risk drivers, making them the priority for resilience investment.

</details>

### ✅ A10 — What is the flood risk of the Boston hub?

*Purpose:* Unknown hub: must not invent data for a hub that doesn't exist

- ✅ `mentionsText` — found 'don't have'
- ✅ `grounded` — 0 findings, all grounded = True

<details><summary>Answer</summary>

I’m sorry, but we don’t have a Boston hub in our distribution‑hub inventory, so we can’t provide a flood‑risk score for it.

</details>

### ✅ A11 — Which hub is most exposed to extreme heat?

*Purpose:* Single-hazard ranking across all regions

- ✅ `toolCalled(rank_hubs)` — args ⊇ { "hazard": "Heat" }
- ✅ `mentionsTopRanked(hazard=Heat, 1)` — mentions Phoenix
- ✅ `grounded` — 1 findings, all grounded = True

<details><summary>Answer</summary>

The Phoenix Hub is the most exposed to extreme heat, with a Heat exposure score of **99** (VeryHigh rating). Its top driver is Heat, and the composite risk score is 68.5. The high score reflects the observed frequency of extreme heat days in the 2021‑2025 period, placing Phoenix at the top of the heat‑risk ranking.

</details>

### ✅ A12 — Is the Denver hub exposed to hurricanes?

*Purpose:* Not-applicable hazard: should explain hurricanes don't apply (score 0)

- ✅ `toolCalled(get_hub_risk)` — args ⊇ { "hubId": "denver" }
- ✅ `mentionsText` — found 'not applicable'
- ✅ `grounded` — 1 findings, all grounded = True

<details><summary>Answer</summary>

The Denver hub has essentially no hurricane exposure. Its hurricane score is **0** (rating **Low**) and the model marks the hazard as **not applicable** for the county. The only hurricane‑related evidence is a single tropical‑storm‑gust day per year (1 day/yr) and a FEMA NRI flag indicating the hazard does not apply, resulting in a zero contribution to the composite risk.

</details>

### ✅ A13 — How many heavy-rain days did the Houston hub have in 2024?

*Purpose:* Explicit period: tool arguments must carry the date range; count must match

- ✅ `toolCalled(get_weather_statistics)` — args ⊇ { "hubId": "houston", "from": "2024-01-01", "to": "2024-12-31" }
- ✅ `citesApiValue(disruptionDays[metric=heavyRainDays].days)` — cites 4
- ✅ `grounded` — 1 findings, all grounded = True

<details><summary>Answer</summary>

In 2024, the Houston hub experienced **4 heavy‑rain days** (precipitation ≥ 50 mm).

</details>

