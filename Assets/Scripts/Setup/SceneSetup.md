# Setup Guide: Onboarding Flow, Transitions, and Pause Menu

This guide details how to configure the newly rebuilt Greetings Canvas onboarding flow, scene transitions, and pause menu in the Unity Editor.

---

## 1. Greetings Profile Canvas Onboarding Setup

The new progressive onboarding canvas consists of 3 distinct pages: **Name Input**, **Age Selection**, and **Buddy Avatar Selection**.

### Script Attachment
1. Attach the `ProfileScreenController` script to your **Greetings** Canvas GameObject (or the `MenuScreenManager` GameObject).
2. Wire up the references in the Inspector as detailed below.

### Section References Mapping

| Category | Field Name | Hierarchy / Scene Target |
| :--- | :--- | :--- |
| **Pages** | Name Page | Drag the `Name` child GameObject under `Greetings`. |
| | Age Page | Drag the `Age` child GameObject under `Greetings`. |
| | Avatar Page | Drag the `Avatar` child GameObject under `Greetings`. |
| **Name Page** | Name Input Field | Drag the `InputField (TMP)` GameObject. |
| | Name Next Button | Drag the `Next` button under the `Name` page. |
| | Name Mascot Speech Text | Drag the Text (TMP) component inside `Mascot Greetings -> Speech -> message bubble`. |
| | Name Mascot Animator | Drag the `Mascot` Animator component on the Name page. |
| | Name Message Bubble | Drag the `message bubble` GameObject (used for the pop-in animation). |
| | Name Mascot Greetings | Drag the `Mascot Greetings` parent GameObject. |
| | Player Name Container | Drag the `Player Name` parent container GameObject. |
| **Age Page** | Age Mascot Speech Text | Drag the Text (TMP) inside `Mascot Greetings -> Speech -> message bubble` on the Age page. |
| | Age Mascot Animator | Drag the `Mascot` Animator component on the Age page. |
| | Age Message Bubble | Drag the `message bubble` GameObject on the Age page. |
| | Age Mascot Greetings | Drag the `Mascot Greetings` parent GameObject on the Age page. |
| | Player Age Container | Drag the `Player Age` parent container GameObject. |
| | Age Previous Button | Drag the `Previous` button on the Age page. |
| | Age Next Button | Drag the `Next` button on the Age page. |
| | Age Answer Slot | Drag the `Age Answer` GameObject (where the selected age pops in). |
| | Age Answer Visuals | Drag the GameObjects/images representing ages 3 to 10 inside the slot array. |
| | Age Buttons Container | Drag the `Content` RectTransform inside `Scroll View -> Viewport` containing children `3`, `4`, `5` etc. |
| **Avatar Page** | Avatar Mascot Speech Text | Drag the Text (TMP) inside `Mascot Greetings -> Speech -> message bubble` on the Avatar page. |
| | Avatar Mascot Animator | Drag the `Mascot` Animator component on the Avatar page. |
| | Avatar Message Bubble | Drag the `message bubble` GameObject on the Avatar page. |
| | Avatar Mascot Greetings | Drag the `Mascot Greetings` parent GameObject on the Avatar page. |
| | Player Avatar Container | Drag the `Player Avatar` parent container GameObject. |
| | Avatar Previous Button | Drag the `Previous` button on the Avatar page. |
| | Avatar Next Button | Drag the `Next` button on the Avatar page. |
| | Avatar Buttons Container | Drag the `Content` RectTransform inside `Scroll View -> Viewport` containing buddy buttons `Khloe`, `Caleb` etc. |
| | Top Right Player Name Text | Drag the `{Player name}` Text component at the top right of the Avatar page. |
| | Top Right Avatar Image | Drag the `Avatar -> Image` component at the top right of the Avatar page. |

### Avatar Sprites Setup
To automate updating the top-right profile picture when selecting a buddy:
1. Select the **Profile Sprites** array in the Inspector.
2. Select all the variant-2 sprites in `Assets/Art/Sprites/Avatars/` (e.g. `Caleb2.png`, `Khloe2.png`, etc.) and drag them into the list.
*The script automatically matches the selected buddy to their profile sprite at runtime!*


---

## 2. Pause Menu & Reusable Click-Pop Setup

### Pause Menu Configuration
1. Attach `PauseMenuController` to the `Menu Pause` Canvas.
2. Assign the references:
   - **Pause Menu Object**: The main `Menu Pause` GameObject.
   - **Content Panel**: The `Content` RectTransform panel (scales up/down).
   - **Open Button**: Settings/Gear button in the HUD.
   - **Close Button**: Red 'X' close button inside the panel.
3. Check **Pause Time** if you want the game time (`Time.timeScale = 0`) to freeze when active.

### Decoupled HUD Triggering
- For gameplay scenes where the Pause Menu is a prefab, attach `OpenPauseMenuTrigger` to the HUD's pause button. It will auto-connect with the active prefab instance.
- Attach `UIButtonPopFeedback` to **any** button in the game (settings gear, close arrow, next) to add a satisfying bouncy click-pop.

---

## 3. Curtain Loading Transitions

### Setup
1. Attach `SceneTransitionManager` to the `TransitionCanvas` prefab.
2. Ensure you have assigned:
   - **Transition Canvas**: `TransitionCanvas` component.
   - **Left / Right Curtains**: Pivots at `(1, 0.5)` and `(0, 0.5)` so they meet perfectly in the center.
   - **Loading Content Group**: CanvasGroup on the loading overlay.
   - **Lesson Number / Title Text**: TMP text components inside the loader overlay.
3. Configure the **Minimum Closed Duration** (e.g. `1.5` seconds) so the loading screen shows for a comfortable, readable amount of time before opening.
