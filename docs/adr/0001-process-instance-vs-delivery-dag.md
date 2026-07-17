# ADR 0001: Process instance (method steps) vs delivery DAG

**Status:** Proposed  
**Date:** 2026-07-17  
**Repo:** [AIGuiders.AgentTaskKnowledge.Core](https://github.com/AI-Guiders/AIGuiders.AgentTaskKnowledge.Core)  
**Context:** TaskKnowledge central store / OOA&D; BA chain (need → journey → story → AC → build); agent orientation «что делать на шаге»

**Related (KB, не этот репо):** agent-notes `knowledge/domains/agent-operations/note-taskknowledge-flow.md`, `playbook-taskknowledge-adr-analytics.md`

---

## Context

1. **TaskKnowledge** уже держит *delivery*: analytics → epic → task DAG → criterion → `route_next` (фокус `epic_id`). Store центральный (`scope_subdir`, primary ≠ KB).
2. В классике BA/UX (BABOK Requirements Life Cycle; journey → story map → story → AC) есть **два объекта**:
   - *что* строим (артефакты требования/работы);
   - *как* ведём изменение (жизненный цикл / метод с гейтами).
3. У агента нет персистентного «я» в модели; внешняя память (TK/Findings/CASA) закрывает факты. Но без **режима метода** агент на каждом ходе снова угадывает: «сейчас аналитика или уже код?».
4. **Не изобретать** span/якоря: CIDE уже канон `AttachmentAnchor` / `CodeAnchor` (cascade-ide ADR 0128 / 0156). TK ссылается, не дублирует словарь координат.
5. Риск: раздуть TK в Jira + BABOK-suite или смешать шаги процесса с delivery-tasks; люди забывают статусы → stale ticket pile.
6. **Product ADRs живут в репо продукта** (как CIDE `docs/adr/`), не в agent-notes KB. KB — EnvInvariant/playbook среды; этот ADR — решение Core/MCP.

---

## Decision

### D1. Две сущности, два дома

| Сущность | Где | Роль |
|----------|-----|------|
| **Process definition** (playbook метода) | **KB** (agent-operations playbook) | Канон шагов, входов/выходов, tools, gates — почти статика |
| **Process instance** (workline) | **TaskKnowledge** store | «Этот epic сейчас на шаге S; gate G» — динамика |
| **Delivery DAG** (tasks) | **TaskKnowledge** store | Что сдать; criterion; blockers |
| **Code anchors** | **CIDE** `AttachmentAnchor` | Кусок кода; TK только ссылается |

### D2. Process instance — первоклассная карточка TK (не task)

**Имя в контракте (закрывает open 3):** **`process`** — `process_get` / `process_advance` / `processes/`.  
Это *workflow-модель* метода (def + instance), **не** workflow-engine (D5). В речи с человеком допустимы синонимы *workline* / *где мы в методе*; в API и путях store — только `process`, без rename до боли.

Минимальный контракт (markdown card, section-meta; имена полей стабильны для MCP позже):

```text
process_instance_id:  <token>           # e.g. wi-anui-cide-live
kind:                 delivery|spike|meta
epic_id:              <epic>|none       # обязателен при kind=delivery; иначе none
process_def_id:       <kb-ref>          # e.g. playbook-agent-delivery
step:                 <step-id>         # current step in definition
status:               active|paused|done
```

**`gate` не хранить на instance.** P/Q живут в PD (D9). Для текущего `step` host/`process_get` **выводит**:

- на входе в step / перед работой: выполняется ли **P** (DoR);
- перед `process_advance` со step: выполняется ли **Q** (DoD).

То есть «gate» = оценка `{P}?` / `{Q}?` по def + evidence/фактам, не отдельное поле карточки. Soft/hard — политика enforcement (D9), не второй SSOT.

**Привязка к дереву (гибрид C — закрывает open 1):**

| kind | Где «живёт» | Зачем |
|------|-------------|--------|
| `delivery` | 1:1 с **epic** (`epic_id` required) | обычная поставка; `task_id` → epic → instance |
| `spike` / `meta` | только `process_instance_id` (**без** epic) | исследование / tooling-разговор; **не** плодить stub-epic |

Анти-зомби: не оставлять process без ключа resolve; не создавать пустой epic «только чтобы повесить process».

Опционально: `entered_at`, `evidence` (ссылки на analytics/task/anchor), `next_hint` (одна строка для агента), `safety_ref` (→ AEE/safety host, см. D7).

Layout: `{store}/processes/{id}.md` рядом с `tasks/`, `analytics/`, `epics/` — **не** файл в `tasks/`.

### D3. Канонический definition (KB) — стартовый скелет шагов

Идентификатор определения (черновик): `playbook-agent-delivery` в agent-notes KB.

| step_id | Цель шага | Типичный выход | Gate «можно дальше» |
|---------|-----------|----------------|---------------------|
| `intake` | Need одной фразой + границы | epic / analytics seed *(spike: instance без epic)* | need сформулирован |
| `analyze` | Analytics card | `analytics/{id}.md` | analytics с criterion/ready |
| `cut` | Нарезка DAG | tasks + blockers + criterion | ≥1 ready или явный next |
| `bind` | Привязка к коду | ссылки на AttachmentAnchor | якоря или explicit N/A |
| `execute` | Работа по `route_next` | code + task status | task done + evidence |
| `verify` | Criterion / AC | verify section | criterion_met |
| `close` | Handoff / checkpoint + **promote** (D10) | handoff / epic done / KB upsert или N/A | instance status=done; generalize рассмотрен |

Шаги **не** являются узлами delivery DAG. Переход шага — отдельное действие (`process_advance` / upsert section), не `task_upsert status=done`.

Колонка «Gate» в таблице выше — черновик **Q** (post) / входа в следующий step; полная грамматика шага — **D9** (`{P} S {Q}`).

### D4. Поведение агента / MCP (направление, не обязательно в 0.3.x)

1. Перед работой по epic: `ensure_store` → **`process_get`** (см. D6) → понять step/gate.
2. `route_next(epic_id=…)` остаётся про **tasks**; ответ/trajectory **учитывает** текущий `step`.
3. Tools: `process_get` (обязательный dig-out после compact), `process_upsert` / `process_advance`; `step` в JSON `meta` у `route_next`.
4. Definition — KB (`read_knowledge_file`); TK — только `process_def_id` + runtime state.

### D5. Границы

- **Не** port BABOK целиком; не User Journey UI в TK.
- **Не** `project_id` как граница store/DAG.
- **Не** новый формат span; bind → CIDE anchors.
- Process definition меняется редко (KB); instance — часто (TK).
- **Не** BPM/Temporal runner: нет автоисполнения фичи; только ориентация + optional soft/hard gate.

### D6. Dig-out после summarization: `process_get` (harness trajectory)

Главный UX для агента после compact / смены треда / «где мы?»:

```text
process_get(workspace_path, task_id? | epic_id? | process_instance_id?)
  → {
      process_instance_id, kind, epic_id?, process_def_id,
      step, status, next_hint,
      pre, post,            # from PD for current step (D9)
      pre_ok?, post_ok?,    # derived: P/Q hold given evidence? (optional)
      # optional: allowed_tools / forbidden_actions for this step
    }
```

**Resolve:**
- `process_instance_id` → карточка напрямую (любой `kind`);
- `epic_id` → instance `kind=delivery` с этим epic (1:1);
- `task_id` → task card → `epic_id` → delivery instance («get_task_process»);
- spike/meta без epic → только по `process_instance_id` (harness/чат держит id).

Агент: *«ага, step=execute, gate=…, next_hint=route_next»* — без перечитывания всего чата.

Host harness (Cursor/CIDE) может вызывать тот же контракт на старте хода; это **не** workflow engine, а **read model режима метода**.

### D7. Advance / автономия — AEE (CIDE), не дубль в TK

**Кто двигает step:** оба (человек и агент) через явный `process_advance` (нет timer/BPM auto-advance). При сомнении агент запрашивает verify у человека.

**Режим «безопасно / опасно»** (доки vs операции с ЦБ и т.п.) — **не** отдельная лестница `open|guided|strict` внутри TaskKnowledge. Канон уже в **CIDE AEE** (контур B / product-specific): ADR 0148, verify ladder, политики verify, `safety.*` / confirm — см. KB `playbook-agent-execution-environment-v1.md`.

| Слой | Ответственность |
|------|-----------------|
| TK `process_*` | step/gate метода; dig-out после compact |
| CIDE AEE / safety | нужна ли human-confirm на акт; verify rung; политика среды |

Process card максимум опциональный `safety_ref` (или эквивалент) → политика host’а; **без** копирования AEE-модели в Core.

**Переиспользование:** при появлении второго потребителя (MCP host вне CIDE, Cursor harness) — **вынести механику safety/autonomy в общую библиотеку** и подключать из CIDE + TK host; не форкать правила в markdown-only эвристиках агента.

### D8. Agent-owned status + anti-ticket-pile

Люди **забывают** менять статусы и плохо помнят «делали ли похожее». AFPM не должен опираться на человеческую гигиену доски (классический провал Jira).

| Норма | Смысл |
|-------|--------|
| **Agent-owned status** | Кто сделал работу (агент) — тот обновляет task/process в store; человек — verify при сомнении (D7), не санитар кликов |
| **Done ≈ evidence** | Предпочтительно связывать завершение с evidence (diff, Finding, handoff, criterion), а не с «кнопкой Done» в UI |
| **Process ≠ tickets** | Шаги метода — process instance; тикет — единица delivery с criterion. Мелкое «посмотрели» → Finding/analytics, не новая task |
| **Перед cut / новым task** | Поиск похожего: Findings / TK / KB; дубль → ссылка + continue/reopen, не клон карточки |
| **Узкий луч** | Ориентация = `process_get` + `route_next(epic_id)`, не чтение всего бэклога |
| **Decay** | Задачи без движения → `parked`/`stale` (suggest при close или hygiene-pass); ворох не обязан быть «зелёным» |

**Не цель:** weekly grooming человеком. **Цель:** агент не тонет в тикетах; store правдив относительно работы, а не относительно памяти оператора.

### D9. DoR / DoD = тройка Хоара на шаге PD

В **process definition** (KB) каждый `step_id` — не «статус + чеклист ради чеклиста», а:

```text
{ P }  S  { Q }
pre:   …   # P = DoR шага (что должно быть правдой до)
do:    …   # S = цель шага / типичные tools
post:  …   # Q = DoD шага (что должно быть правдой после)
```

| | Метод (PD step) | Delivery (task) |
|--|-----------------|-----------------|
| **P / DoR** | можно входить в step / advance сюда | task ready: criterion ясен, blockers clear, analytics |
| **S** | работа на шаге | работа агента по task |
| **Q / DoD** | можно уходить со step | evidence + criterion_met; код → AEE где нужно (D7) |

**Instance** хранит `step` (+ факты/evidence); **P/Q не копировать** и **не дублировать полем `gate`** — читать/выводить из `process_def_id` + `step` (D2).

**Enforcement (закрывает open 2 и 5):**

- По умолчанию **soft**: `process_advance` проходит с `warnings`, если P/Q не закрыты.
- **Hard** — точечно: `close` / опасные defs через AEE/`safety` (D7); не корпоративный 15-пунктовый DoR.
- Findings на `bind`→`execute`: пункт **P** для `execute` (или soft hint), не отдельная сущность и не hard по умолчанию (D8: мелкое → Finding, не тикет).
- Formal verification Хоара не требуется — нужна **общая грамматика** для агента после compact.

`process_get` может отдавать краткие `pre`/`post` текущего step из def (опционально в фазе C+).

### D10. Generalize / promote — иначе AFPM = дневник без капитала

Шаги + заметки по task (ошибки, находки, Findings) дают агенту **«где я»** и **«что было в ходе»**. Мало смысла, если локальное **не обобщается** в то, что знает *контур* после этого чата.

| Слой | Что | Срок |
|------|-----|------|
| Task / process notes | ошибки, находки «в этом ходе» | короткий |
| Findings / analytics | AS IS + TO BE по эпику | средний |
| KB / PD / EnvInvariant / playbook | паттерн, правка P/Q, reusable норма | долгий |

**Где в методе:** не обязательно отдельный гигантский step. Обычно пункт **Q шага `close`** (иногда `verify`): «есть ли что promote?»

| Исход promote | Действие |
|---------------|----------|
| Повтор / дорогое падение / новый invariant | upsert KB / правка PD (`pre`/`post`) / Finding→playbook |
| Уникальный шум хода | explicit **N/A** (не плодить тикет и не молчать) |
| Уже есть в KB | ссылка + не дублировать (D8 search) |

**Enforcement:** soft (D9) — warning при `close` без promote/N/A; не hard-ритуал на каждый мелкий spike.

**Связь:** dig-out (D6) = ориентация; D10 = обучение контура. Без D10 stateful-over-stateless копит шум, не капитал. Перед следующим `cut` search (D8) как раз ест плоды promote.

---

## Consequences

### Positive

- Агент получает **режим метода**, не только список tasks.
- После summarization: один вызов `process_get` / get_task_process → step без восстановления всего диалога.
- Канон метода в KB; runtime instance в central TK store.
- Product ADR рядом с кодом Core (как CIDE).
- Автономия не плодится: AEE SSOT; при необходимости — shared lib.
- Статусы не зависят от человеческой памяти кликов; меньше ложной «Jira-доски».
- DoR/DoD как `{P} S {Q}` — короткая machine-oriented грамматика шага, не ceremony.
- Локальные находки могут стать капиталом контура (promote), а не только transcript noise.

### Negative / trade-offs

- Ещё один тип карточки и (позже) MCP surface.
- Риск «процесс ради процесса», если gates / P/Q пустые.
- Пока MCP не отдаёт `step`, дисциплина по карточке + man/playbook.
- `process_advance` в MCP host без CIDE должен либо no-op safety, либо подключить shared lib — иначе «дырявый» strict.
- Agent-owned status требует дисциплины агента (и soft checks); иначе store всё равно врёт, только уже от агента.
- Soft-by-default можно «проскочить» пустой Q — нужна культура evidence (D8), не только warnings.
- Promote без дисциплины → шум в KB; с гипер-дисциплиной → ceremony. Нужен soft + N/A.

---

## Alternatives considered

| Вариант | Почему не выбран |
|---------|------------------|
| Только playbook в KB, без instance | Нет персистентного «где мы в методе» после compact. |
| Шаги процесса = обычные tasks | Смешивает method и delivery. |
| Process целиком в CASA | Другой контур; TK = workflow store. |
| ADR только в agent-notes KB | Путает product decision с KB-статикой; см. размещение этого файла. |
| BPM / auto-advance runner | Ломает партнёрство; overkill. Нужен dig-out read model (D6), не движок. |
| Только chat handoff без process_get | Handoff хорош, но не API; после compact агент снова не знает step. |
| Своя autonomy-лестница в TK (`open\|guided\|strict`) | Дубль AEE / `safety.*`; ломает контур B. Ссылка + optional shared lib. |
| Человек как SSOT статусов (классический трекер) | Забывает кликать; доска врёт; агент тонет в stale tickets. → D8. |
| Тикет на каждый микрошаг / мысль | Ворох без criterion; дубли «похожее». → Finding + process + search before cut. |
| Строго 1 process : 1 epic всегда (чистый A) | Spike/meta → stub-epic или process «негде жить» (зомби). → D2 гибрид C. |
| Process всегда без epic (чистый B) | Слабый якорь к delivery DAG; `route_next` и метод расходятся. |
| Primary id = `engagement` / `task journey` | CRM / UX-journey коллизии; путает с user journey и task card. → D2: `process`. |
| Primary id = `workline` | Хорош в речи; смена API до dogfood дороже пользы. Синоним в man ок. |
| Primary id = `workflow` | Честно, но тянет Camunda/Jira Workflow. Оставляем «workflow-модель» в прозе, id = `process`. |
| Длинный корпоративный DoR/DoD как отдельный продукт | Ceremony; человек всё равно не кликает. → D9: коротко в PD как P/Q; soft default. |
| Формальная верификация Хоара в MCP | Overkill. Нужна грамматика и soft/hard enforcement, не proof checker. |
| Только task notes / Findings без promote | Дневник хода; следующий агент снова на те же грабли. → D10. |
| Обязательный KB-write на каждый close | Ceremony и шум в KB. → soft + explicit N/A. |

---

## Implementation notes (после Accept)

**Фаза A:** Accept; KB playbook definition с `pre`/`do`/`post` на step (D9); template process card; EnvInvariant.  
**Фаза B:** `processes/*.md` dogfood (`anui-cide-live`).  
**Фаза C:** MCP `process_get` → step/gate/next_hint (+ optional pre/post); `process_advance` soft warnings / hard where AEE; `route_next` meta `processStep`.  
**Фаза D (D8):** man/trajectory: agent обновляет status; перед cut — finding/TK search; optional parked/stale hygiene.  
**Фаза E (D10):** Q/`close`: promote → KB/PD или N/A; soft warning если ни то ни другое.

**Критерий «агенту ок после compact»:** по `task_id` или `epic_id` один MCP-вызов возвращает текущий step без чтения transcript.  
**Критерий anti-pile:** новый task без поиска похожего = anti-pattern; open без criterion = не создавать.  
**Критерий P/Q:** каждый step в PD имеет непустые pre/post (хотя бы одной строкой).  
**Критерий capitalize:** `close` без promote и без N/A = anti-pattern.
---

## Open questions

1. ~~Один process на epic vs workline без epic~~ → **D2:** гибрид C — `delivery`↔epic 1:1; `spike`/`meta` без epic (анти-зомби / анти-stub).
2. ~~Gates soft vs hard~~ → **D9:** soft по умолчанию; hard точечно (`close` / AEE safety).
3. ~~Имя~~ → **D2:** primary **`process`** (API/store); в прозе — workflow-модель, не engine; *workline* — человеческий синоним.
4. ~~Кто двигает step~~ → **D7:** оба + явный advance; режим confirm → AEE/safety (shared lib при переиспользовании).
5. ~~Findings на bind/execute~~ → **D9:** пункт **P** шага `execute` (soft hint); не hard по умолчанию.

---

## References

- C. A. R. Hoare — An Axiomatic Basis for Computer Programming (`{P} S {Q}`)
- IIBA BABOK — Requirements Life Cycle Management; DoR/DoD как практика delivery
- NN/g — User story mapping; journey vs flow
- cascade-ide ADR 0128 / 0156 — AttachmentAnchor / CodeAnchor
- cascade-ide ADR 0148 — AEE verification ladder / native tooling; `docs/design/naming-layers-v1.md` (`safety.*`)
- agent-notes: `note-taskknowledge-flow.md`, `playbook-taskknowledge-adr-analytics.md`, `playbook-agent-execution-environment-v1.md`
