# Original Codex Starter Prompt

This is the historical starter prompt. For the active milestone and engine status, open [Current state](CURRENT_STATE.md).

> We are starting an original standalone MOBA project. Please read `AGENTS.md`, `README.md`, and all Markdown files in `docs/` before writing code. This folder is a context handoff from a long game-design conversation; the five hero specifications are designer-authored intentions, and unresolved choices are not approved features.
>
> Our goal is **Movement Lab v0.01**, NOT a full game. First inspect the local development environment and report whether Unreal Engine 5, Visual Studio/C++ compiler, and relevant dependencies are available. We previously leaned toward Unreal C++/Blueprint/GAS, but this has not been verified; don't download huge tools or choose a different engine without talking to me.
>
> If the environment is suitable, create the minimum clean Unreal project (or a clear project scaffold if the editor isn't installed) and implement a small top-down/right-click-to-move sandbox using placeholders and tree-like obstacles. Expose movement speed, turning rate and stopping behavior for tuning. Make a simple repeatable movement test area and provide steps to launch/test on Windows. No networking, heroes, minions or shop yet. Keep the patch focused, run checks possible in the environment, and tell me honestly what was and wasn't tested.
>
> Please work as a technical partner: do as much code/setup as possible, then give me short, clear steps for anything that must be done directly on my machine. Preserve meaningful design decisions in docs and commit checkpoints to Git when appropriate.

## Setup on desktop

1. Download `MOBA-Project.zip` from the ChatGPT conversation.
2. Extract the ZIP into a folder you own (e.g. `Documents/Games/MOBA-Project`).
3. Open the ChatGPT desktop app, choose **Codex**, then select **Add new project** / open a local folder and choose the **MOBA-Project** root folder.
4. Start the task using the prompt above.

If using Codex Cloud instead, put the files into a GitHub repository you control and connect that repository to Codex Cloud. Codex history itself is not automatically migrated from this ChatGPT chat.
