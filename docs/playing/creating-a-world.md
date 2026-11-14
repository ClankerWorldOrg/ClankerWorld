---
title: Creating a world
type: player-guide
status: active
updated: 2026-11-14
---

# Creating a world

Create a world, configure its models and place the first Town.

## Create your first Town

On a fresh server, **Continue** opens **New World** so you can choose the map
before placing your first Town. Existing worlds with founders or other work
still open normally.

1. Choose **New World**. Enter a name and seed and choose Small or Medium. Other
   map options are under **+ More options** (see
   [More New World options](#more-new-world-options)).
2. Wait for the preview. Changing an option updates it; **Create World** becomes
   available when the preview for the current choices succeeds.
   Different map sizes or generation choices can reuse a seed and keep separate
   worlds. Changing only the name does not create another copy of the same map.
3. Create the world. It opens paused at 06:00 on Spring 1, Year 1, in daylight.
   Choose a rough site for its first Town;
   greener shading and the hovered tips show nearby food, fertile ground, wood,
   stone and open space for Roads. These are suggestions only: the game checks
   whether the starter layout fits after you choose a site. It then places two
   Houses, a Warehouse, Farmhouse, Blacksmith and Roads. You can choose another
   site before placing the founders, including after removing an empty starter
   Farmhouse or Blacksmith. **Redo Town site** replaces the starter layout and
   its Roads together.
4. Configure and place **four starting agents**. Choose each agent's provider
   (OpenAI, Ollama Cloud or Anthropic), key and model. A saved key can be
   reused, or you can add another for the same provider. Then pick the model
   from the game's short list for that provider, newest at the top; Anthropic
   starts on Claude Haiku 5.5. Models your key can't use are greyed out; if
   it can't use the model shown, the picker asks you to choose another.
   **Type a model name...** covers any other model. **Thinking** sets how much
   the model thinks before it answers: **Model default** leaves the provider's
   own setting, and **Low**, **Medium** or **High** asks for that much. More
   thinking takes longer and uses more paid tokens. The agents start in two
   households, not two forced couples or biological families.
5. Press **Start World** when all four are ready. Placing the fourth agent does
   not start time automatically. Before starting, placements can be moved or
   undone; incomplete setup can be saved and resumed later.

Keys stay on the server installation, separately from world saves. Cloud
providers charge you for usage. While paused, open **Settings → World → Routine
helper** to choose **Off**, **Jev** or **OpenAI Decisions**, pick its model and
press **Apply helper**. Decisions uses your default OpenAI key or a named saved
OpenAI key you choose; Jev uses your default TypeSafe key or a named saved
TypeSafe key you choose. New Jev selections start on **jev-1.14.0**; a world
already using **jev-1.13.0** keeps that model until you change it. Switching helpers keeps
memories and their scores. The helper serves every adult and elder, including
people born in the world; children keep their own model. Conversations, identity
and wills still use each person's own model. Decisions currently offers `gpt-6-luna`; **Type a
model name...** also remains available.

The helper answers only when every available action is about food, clothing,
warmth, care or waiting. An agent doing everyday work can still have bigger
choices available, so its personal model may answer instead. Applying a helper
selects it without checking its connection. **Test helper · 1 paid call** checks
the selected model and saved key immediately, without waiting for routine work.
The result explains a usable reply or a connection, key or model problem; each
sent test counts toward the model-call limit. A successful test leaves the
world's helper choice and its agents unchanged.

To save keys before placing any agents, open **Settings → Game → API keys**.
Choose OpenAI, Ollama Cloud, Anthropic or **TypeSafe (Jev)**, name the key, paste it and press
**Save API key**. You can save each provider's keys from Main Menu Settings once this device is
connected and paired. Saving a key does not run a model or place an agent.
Choose the saved key later in **Add Agent**, an agent's **Model** panel or
**Routine helper**. TypeSafe keys are for the Jev helper.
**Open model settings** in that panel opens the agent's model in World
Settings. To delete a saved key, choose it under **API keys** and press
**Delete saved key**; an agent's model settings also offer deletion.
A key used by the routine helper cannot be deleted, including a default key
in World defaults. Choose another helper key or turn the helper off first.

To check a selected model, press **Test model · 1 paid call** in Add Agent or
an agent's Model panel. It uses the chosen **Thinking** setting too. This sends one real request and counts toward the
model-call limit, even if the provider times out or rejects it.
A key pasted for this check is not saved. The result tells you whether the
provider returned a reply the game can use; it does not show provider error
details.

## More New World options

The simple preset uses 50% water, Normal forest/mountains/rivers/resources,
Balanced climates, east/west wrapping and latitude cooling. Choose
**+ More options** to set water from 20% to 80% with a slider, pick Low, Normal
or High forest, mountains, rivers and resources, and change climates, wrapping
or latitude cooling. **Reset these options** restores the preset and Small size
without changing the name or seed. The preview updates after
changes; Create World waits for a matching preview. Small and Medium are
available; larger worlds remain unavailable pending their support checks.
If a Settings action is still finishing when you open New World, its preview
starts automatically after that action completes.
**Reroll** chooses another seed and clears the old map while its replacement
loads. You can reroll during generation; the preview will follow your latest
seed. If the world name is missing, enter it and choose **Preview again**.
