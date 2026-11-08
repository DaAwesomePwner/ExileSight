# Map-event notifications

Passive Client.txt notifications for mercenary first approaches, the Nameless Seer,
Reflecting Mist and discoverable hideouts. Display names, categories and initial
switch values come exclusively from `poe1/events`. A missing pack disables display.
Matching and first-approach filtering remain in the Core English log parser.

Press Ctrl+Shift+E to open the local event settings window. An existing persisted
binding with the same ID (`events.settings`) takes precedence. Each checkbox writes
`enabled.<pack event id>` in the `events` feature settings section. Hideouts default
off; the other shipped definitions default on. Changes apply immediately; enabling
an event does not replay a previously observed occurrence.

One click-through, no-activate toast is visible for five seconds. Up to four entries
may wait; pending entries older than fifteen seconds expire. New area instances,
log gaps and source changes clear presentation. A gap or source change requires a
fresh live AreaGenerated before accepting more notifications; replay never alerts.
Dedupe keys are area ID, seed and event kind. The latest 256 newly encountered
instances are retained in memory, including revisits and gap recovery. New instance
arrivals evict the oldest; source changes discard all keys. An evicted instance can
notify again if revisited.

Hideout attribution assumes `Spawning discoverable Hideout` follows the matching
`Generating level` line. The diagnostic contains no instance key; before generation
it is suppressed if no area is known, otherwise it is attributed to the current
area. An out-of-order diagnostic can therefore be missed or attributed to the prior
instance. Hideout notifications remain off by default.
Neither player names nor raw log/chat text enter presentation or persistence.

Behaviour reference: Exile-UI (MIT), `modules/client log.ahk` 578–592 and
`modules/qol tools.ahk` 757–771. Notification/queue code is ExileSight-authored.
