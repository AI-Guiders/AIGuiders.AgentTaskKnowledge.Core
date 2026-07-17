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
5. Риск: раздуть TK в Jira + BABOK-suite или смешать шаги процесса с delivery-tasks.
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

Минимальный контракт (markdown card, section-meta; имена полей стабильны для MCP позже):

```text
process_instance_id:  <token>           # e.g. wi-anui-cide-live
epic_id:              <epic>            # связь с delivery DAG
process_def_id:       <kb-ref>          # e.g. playbook-agent-delivery
step:                 <step-id>         # current step in definition
gate:                 <gate-id|none>    # what must hold to advance
status:               active|paused|done
```

Опционально: `entered_at`, `evidence` (ссылки на analytics/task/anchor), `next_hint` (одна строка для агента), `safety_ref` (→ AEE/safety host, см. D7).

Layout: `{store}/processes/{id}.md` рядом с `tasks/`, `analytics/`, `epics/` — **не** файл в `tasks/`.

### D3. Канонический definition (KB) — стартовый скелет шагов

Идентификатор определения (черновик): `playbook-agent-delivery` в agent-notes KB.

| step_id | Цель шага | Типичный выход | Gate «можно дальше» |
|---------|-----------|----------------|---------------------|
| `intake` | Need одной фразой + границы | epic stub / analytics seed | need сформулирован |
| `analyze` | Analytics card | `analytics/{id}.md` | analytics с criterion/ready |
| `cut` | Нарезка DAG | tasks + blockers + criterion | ≥1 ready или явный next |
| `bind` | Привязка к коду | ссылки на AttachmentAnchor | якоря или explicit N/A |
| `execute` | Работа по `route_next` | code + task status | task done + evidence |
| `verify` | Criterion / AC | verify section | criterion_met |
| `close` | Handoff / checkpoint | handoff или epic done | instance status=done |

Шаги **не** являются узлами delivery DAG. Переход шага — отдельное действие (`process_advance` / upsert section), не `task_upsert status=done`.

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
      process_instance_id, epic_id, process_def_id,
      step, gate, status, next_hint,
      # optional: allowed_tools / forbidden_actions for this step
    }
```

**Resolve:**
- `process_instance_id` → карточка напрямую;
- `epic_id` → instance с этим epic (1:1 по D2);
- `task_id` → task card → `epic_id` → instance («get_task_process»).

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

---

## Consequences

### Positive

- Агент получает **режим метода**, не только список tasks.
- После summarization: один вызов `process_get` / get_task_process → step без восстановления всего диалога.
- Канон метода в KB; runtime instance в central TK store.
- Product ADR рядом с кодом Core (как CIDE).
- Автономия не плодится: AEE SSOT; при необходимости — shared lib.
- Статусы не зависят от человеческой памяти кликов; меньше ложной «Jira-доски».

### Negative / trade-offs

- Ещё один тип карточки и (позже) MCP surface.
- Риск «процесс ради процесса», если gates пустые.
- Пока MCP не отдаёт `step`, дисциплина по карточке + man/playbook.
- `process_advance` в MCP host без CIDE должен либо no-op safety, либо подключить shared lib — иначе «дырявый» strict.
- Agent-owned status требует дисциплины агента (и soft checks); иначе store всё равно врёт, только уже от агента.

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

---

## Implementation notes (после Accept)

**Фаза A:** Accept; KB playbook definition; template process card; обновить EnvInvariant (ссылка сюда).  
**Фаза B:** `processes/*.md` dogfood (`anui-cide-live`).  
**Фаза C:** MCP `process_get` (resolve task_id|epic_id|id) → step/gate/next_hint; затем `process_advance` + `route_next` meta `processStep`.  
**Фаза D (D8):** man/trajectory: agent обновляет status; перед cut — finding/TK search; optional parked/stale hygiene (не weekly human grooming).

**Критерий «агенту ок после compact»:** по `task_id` или `epic_id` один MCP-вызов возвращает текущий step без чтения transcript.  
**Критерий anti-pile:** новый task без поиска похожего = anti-pattern; open без criterion = не создавать.
---

## Open questions

1. Один process на epic vs workline без epic? *(ещё обсуждаем)*
2. Gates soft vs hard в MCP? *(ещё обсуждаем)*
3. Имя: `process` vs `workline` vs `engagement`?
4. ~~Кто двигает step~~ → **D7:** оба + явный advance; режим confirm → AEE/safety (shared lib при переиспользовании).
5. Findings на `bind`/`execute`: обязательный gate? *(ещё обсуждаем)*

---

## References

- IIBA BABOK — Requirements Life Cycle Management
- NN/g — User story mapping; journey vs flow
- cascade-ide ADR 0128 / 0156 — AttachmentAnchor / CodeAnchor
- cascade-ide ADR 0148 — AEE verification ladder / native tooling; `docs/design/naming-layers-v1.md` (`safety.*`)
- agent-notes: `note-taskknowledge-flow.md`, `playbook-taskknowledge-adr-analytics.md`, `playbook-agent-execution-environment-v1.md`
