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

Опционально: `entered_at`, `evidence` (ссылки на analytics/task/anchor), `next_hint` (одна строка для агента).

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

1. Перед работой по epic: `ensure_store` → прочитать **process instance** (или создать на `intake`/`analyze`).
2. `route_next(epic_id=…)` остаётся про **tasks**; ответ/trajectory **учитывает** текущий `step`.
3. Tools (фаза 2): `process_upsert` / `process_advance` / `step` в JSON `meta` у `route_next`.
4. Definition — KB (`read_knowledge_file`); TK — только `process_def_id` + runtime state.

### D5. Границы

- **Не** port BABOK целиком; не User Journey UI в TK.
- **Не** `project_id` как граница store/DAG.
- **Не** новый формат span; bind → CIDE anchors.
- Process definition меняется редко (KB); instance — часто (TK).

---

## Consequences

### Positive

- Агент получает **режим метода**, не только список tasks.
- Канон метода в KB; runtime instance в central TK store.
- Product ADR рядом с кодом Core (как CIDE).

### Negative / trade-offs

- Ещё один тип карточки и (позже) MCP surface.
- Риск «процесс ради процесса», если gates пустые.
- Пока MCP не отдаёт `step`, дисциплина по карточке + man/playbook.

---

## Alternatives considered

| Вариант | Почему не выбран |
|---------|------------------|
| Только playbook в KB, без instance | Нет персистентного «где мы в методе» после compact. |
| Шаги процесса = обычные tasks | Смешивает method и delivery. |
| Process целиком в CASA | Другой контур; TK = workflow store. |
| ADR только в agent-notes KB | Путает product decision с KB-статикой; см. размещение этого файла. |
| Новый span-hash в TK | Уже есть AttachmentAnchor (0128). |

---

## Implementation notes (после Accept)

**Фаза A:** Accept; KB playbook definition; template process card; обновить EnvInvariant (ссылка сюда).  
**Фаза B:** `processes/*.md` dogfood (`anui-cide-live`).  
**Фаза C:** MCP `process_*` + `route_next` meta `processStep`.

---

## Open questions

1. Один process на epic vs workline без epic?
2. Gates soft vs hard в MCP?
3. Имя: `process` vs `workline` vs `engagement`?
4. Кто двигает step: человек / агент / оба?
5. Findings на `bind`/`execute`: обязательный gate?

---

## References

- IIBA BABOK — Requirements Life Cycle Management
- NN/g — User story mapping; journey vs flow
- cascade-ide ADR 0128 / 0156 — AttachmentAnchor / CodeAnchor
- agent-notes: `note-taskknowledge-flow.md`, `playbook-taskknowledge-adr-analytics.md`
