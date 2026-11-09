# UV Mask Maker V4 User Manual

This manual provides instructions, workflows, and configuration details for **UV Mask Maker V4**, a Unity editor extension designed to create, edit, and apply mask textures directly on 3D models within the Unity Editor.

---

## 1. Product Overview

### 1.1 Purpose and Summary
**UV Mask Maker V4** enables users to select polygons and UVs or hand-paint directly on 3D meshes (such as VRChat avatars and clothing) in the SceneView to generate and assign black-and-white mask textures.

By eliminating round-trips between external image editing software (e.g., Photoshop, CLIP STUDIO PAINT) and Unity, you can inspect changes in real time and complete the entire workflow—from mask generation to material assignment—within the Unity Editor.

### 1.2 System Requirements and Prerequisites
- **Supported Environment**: Unity 2022.3 LTS or later
- **Target Objects**: GameObjects containing `SkinnedMeshRenderer` or `MeshRenderer` components
- **Supported Shaders**: Shaders featuring mask texture input slots (e.g., lilToon, Poiyomi Toon Shader, Sunao Shader)
- **Mesh Requirements**: Target meshes must have valid primary UV layout (UV0)

---

## 2. Interface Overview and Component Layout

The interface is structured into the following operational areas to maximize workflow efficiency:

```
+-------------------------------------------------------------------+
| Main Window (EditorWindow / Tool Tab)                             |
| [Top Bar: Tool Name / Version / ↻ / Language(JA) / ? / ⋮]         |
| +---------------------------------------------------------------+ |
| | [Session Banner: Target Name / End (Esc)] (Working only)      | |
| | [Target Mesh: Picker / Switch in Same Avatar ▼]               | |
| | [UV View (2D UV layout preview / picking / painting)]         | |
| | [Export Settings: File Name / Resolution / Channels / ▶ Details] |
| +---------------------------------------------------------------+ |
| [Output Dock: Settings summary chips] (Working only)              |
| [Footer Primary Button: Start Editing / Export (Ctrl+Enter)]      |
| [Status Bar: Feedback message / Apply button]                     |
+-------------------------------------------------------------------+

In SceneView:
  - Scene Toolbar (Capsule) (Floating toolbar, freely draggable in SceneView)
  - Radial Menu (Summoned directly under cursor by holding C)
  - Feedback & Notifications (Toast messages / guidance hints / modifier badges)
```

### 2.1 Main Window (Tool Tab)
Opened via top menu: **`dennokoworks` > `MaskMaker V4`**.
The window adapts cleanly between two operational states: **Idle** and **Working**.

- **Top Bar**:
  - **Tool Info**: Displays tool name, version, and a reload button (`↻`) to check for updates.
  - **Language Toggle**: Switches between `EN` and `JA` (displays the target language on the button).
  - **Manual (`?`)**: Opens the included user manual.
  - **Settings Menu (`⋮`)**: Opens Preferences, Project Settings, update checks, or the release page.
- **Session Banner (Working only)**: Displays the active target mesh name and an **"End"** button (or press `Esc`) to exit editing at any time.
- **Target Mesh Card**:
  - Selects the target Renderer. Automatically follows Hierarchy selection; selecting a parent GameObject displays a candidate list of child Renderers (click to select, double-click to start).
  - During editing, a dropdown (`▼`) allows instant switching between Renderers on the same avatar, alongside submesh and UV channel selectors.
- **UV View (Working only)**: Inspects and edits the 2D UV layout with direct selection and painting. Supports base texture overlay, zoom reset (`F`), UV layout wireframe export (camera icon), and drag-and-drop import of external mask images.
- **Export Card (Working only)**: Configures texture file name, resolution choices (512–4096), and color channels (Mask / R / G / B / A). Expand `▶ Details` to configure Invert, Padding (0–32px), Save Inverted Too, and Save to Mask/ subfolder.
- **Output Dock / Footer**: Displays summary chips for output settings and the primary action button ("Start Editing" in Idle, "Export" in Working).
- **Status Bar**: Shows action results, the 1-click "Apply" button, and session end reasons (Esc, scene changed, target deleted, etc.).

### 2.2 Scene Toolbar (Capsule)
Floating, draggable toolbar (capsule) that appears in the SceneView when editing begins:
- **Mode Toggle**: Switches between Selection Mode and Paint Mode.
- **Dynamic Segment**:
  - **Selection Mode**: Switches selection granularity between Island, Part, and Face.
  - **Paint Mode**: Selects Brush, Rectangle fill, Lasso fill, or Eraser (displays current brush size value).
- **Utilities**: Toggles Symmetry Editing (X-Mirror), X-Ray (Translucent) view, Target Isolation, and Pause (Space).
- **History & Actions**: Executes Undo, Redo, Clear Selection, and Quick Export.

### 2.3 Radial Menu
Holding the **`C` key** in the SceneView summons a circular context menu directly under your mouse cursor. Move the cursor toward the desired tool and release the key to switch tools instantly without moving your cursor to screen edges.

### 2.4 Notifications and Scene Feedback
Toast notifications appear temporarily on-screen upon completing an export or specific actions. Clicking **`Apply`** inside the notification automatically assigns the texture to the appropriate material slot. In-scene guidance hints and modifier key badges also provide real-time feedback.

---

## 3. Basic Workflow (Quick Start)

The standard procedure for selecting mesh regions, exporting a mask, and applying it to a material:

```
[Select in Hierarchy] → [Right-Click to Start] → [Click Mesh to Select] → [Ctrl+Enter to Export] → [Click Apply] → [Esc to Finish]
```

### Step 1: Start Editing
1. In the Hierarchy, select the GameObject containing the mesh you wish to mask (e.g., outfit or avatar component).
2. Right-click the object and select **`dennokoworks` > `MaskMaker V4`**.
   *(Alternatively, open the main window and click the **Start** button).*
3. Non-target scene objects are temporarily hidden (isolated), and the SceneView camera automatically frames the target mesh.

### Step 2: Define Mask Areas
1. In the SceneView, **Left-Click** on the mesh surface you want to mask:
   - Clicking an unselected region adds it to the mask.
   - Clicking an already selected region removes it.
   - **Double-Click** to select an entire connected mesh component.
2. For symmetrical components (e.g., sleeves or shoes), press the **`X` key** to enable Symmetry Editing. Selecting one side automatically selects the corresponding geometry on the opposite side.

### Step 3: Export and Assign to Material
1. Press **`Ctrl + Enter`** (or click the Export button on the main window / floating toolbar).
2. Once the image is generated, a notification toast appears on-screen.
3. Click **`Apply`** within the notification. The tool inspects shader properties and assigns the texture to the appropriate mask slot.
   *(If multiple candidate slots exist, choose the desired slot from the displayed list).*

### Step 4: Finish Editing
When done, press **`Esc`** in the SceneView or click the **`End`** button in the main window's session banner to complete the editing session.

---

## 4. Feature Summary Table

| Category | Feature | Description |
| :--- | :--- | :--- |
| **Selection** | **Click Selection** | Toggle selection on and off by clicking mesh regions |
| | **Drag Selection** | Continuously select or deselect polygons along the cursor path |
| | **Part Selection** | Double-click to select an entire connected geometric component |
| | **Granularity Switching** | Switch selection scale between Island, Part, and Face modes |
| | **Grow and Shrink** | Shift + Mouse Wheel to expand or shrink the selection across adjacent faces |
| | **Symmetry Editing** | Press X to mirror selections and paint strokes across the mesh's X-axis |
| | **Select All / Invert / Clear** | Ctrl+A to select all, Ctrl+I to invert selection, and Clear via menu |
| **Painting** | **Paint Tools** | Brush, Rectangle fill, Lasso fill, and Eraser tools |
| | **Brush Size Adjustment** | Ctrl + Right-Drag to dynamically adjust brush radius under the cursor |
| | **Instant Eraser** | Hold Ctrl while painting with any tool to temporarily erase |
| **View** | **Mesh Mask Overlay** | Renders mask areas and UV seam lines directly on the 3D surface |
| | **UV View** | Inspect and edit 2D UV layouts with direct selection and painting |
| | **Base Texture Overlay** | Overlays the original model texture as the background of the UV View |
| | **Target Isolation** | Temporarily hides unselected scene objects during editing |
| | **X-Ray View** | Renders geometry translucent to view and select occluded backfaces |
| | **Pause Picking** | Hold Space to suspend tool inputs and use default camera controls |
| **Export** | **Mask Texture Export** | Generates linear black-and-white PNG mask textures at specified resolutions |
| | **Padding (Dilation)** | Dilates mask boundaries outward to prevent texture filtering seams |
| | **Inverted Mask Export** | Exports inverted masks, or saves normal and inverted versions simultaneously |
| | **Save to Mask/ Subfolder** | Automatically creates and organizes exports inside a Mask/ subfolder |
| | **Channel Packing** | Injects masks directly into specific channels (R, G, B, or A) of existing textures |
| | **Safe Overwriting** | Re-exporting overwrites previous files so linked materials update automatically |
| | **Material Auto-Apply** | 1-click shader slot assignment from post-export notification toasts |
| | **UV Wireframe Export** | Exports wireframe layout PNG images for external paint programs |
| | **Import Mask Image** | Drag and drop black-and-white images into the UV View to merge into paint |
| **Workflow** | **Radial Menu** | Hold C to summon a circular tool picker under the cursor |
| | **Scene Toolbar** | Floating, movable toolbar (capsule) inside the SceneView |
| | **End Session** | Press Esc or click "End" in the window banner to safely finish editing |
| | **Switch Target in Avatar** | Switch directly between Renderers of the same avatar via the ▼ menu |
| | **Unified Undo / Redo** | Full history tracking for selections, paint strokes, and settings via Ctrl+Z / Ctrl+Y |
| | **Auto-Save & Restore** | Persists edit states across scene changes, recompiles, and Unity restarts |
| | **Multi-Language Support** | Instant toggle between Japanese (`JA`) and English (`EN`) |
| | **Help & Settings Menu** | Access manual (`?`) and Settings (Preferences / Project Settings) from the top bar |

---

## 5. Detailed Feature Guide

### 5.1 Selection Mode

Selection Mode assigns mask coverage based on the underlying polygon geometry. Press **`Tab`** to switch between Selection Mode and Paint Mode.

#### Selection Granularity
Switch using keys **`1` / `2` / `3`**, the toolbar, or the Radial Menu:
- **1: Island**: Selects by UV island boundaries (seams). Recommended for quickly selecting distinct fabric or garment sections.
- **2: Part**: Selects connected geometric mesh elements. Double-clicking any polygon triggers the same action.
- **3: Face**: Selects individual triangle polygons. Useful for trimming small sections from an island or part.

*Note: Changing the selection granularity preserves your active selection.*

#### Selection Controls
- **Smart Toggle**: Clicking unselected areas adds them; clicking selected areas removes them.
- **Drag Selection**: Drag with the left mouse button held down to select or deselect all geometry under the cursor path.
- **Modifier Keys**:
  - `Shift` + Click / Drag: Forces addition to the selection.
  - `Ctrl` + Click / Drag: Forces removal from the selection.
- **Grow and Shrink**:
  - `Shift` + Mouse Wheel Up: Expands the selection outward by one adjacent polygon step.
  - `Shift` + Mouse Wheel Down: Contracts the selection inward by one step.
- **Symmetry Editing (X-Mirror)**:
  - Press `X` to toggle symmetry.
  - Polygons mirrored across the mesh's local X=0 plane are selected or deselected simultaneously.

---

### 5.2 Paint Mode

Paint Mode provides freehand drawing capabilities independent of polygon topology. Press **`Tab`** to toggle between Paint Mode and Selection Mode.

#### Paint Tools
Switch using the Radial Menu (hold `C`) or hotkeys:
- **Brush (`B`)**: Projects a circular brush onto the 3D surface.
- **Rectangle (`M`)**: Fills a rectangular screen-space bounding box.
- **Lasso (`L`)**: Fills an arbitrary freehand enclosed region.
- **Eraser (`Shift + B`)**: Erases painted strokes.

#### Brush Size Adjustment
- **`Ctrl` + Right-Drag**: Drag horizontally on the mesh to smoothly resize the brush circle under your cursor.
- **`[` / `]` Keys**: Press `[` to decrease and `]` to increase brush diameter.

#### Instant Eraser
Holding **`Ctrl` while painting with any tool temporarily switches to the eraser**. Releasing `Ctrl` returns to the active tool.

---

### 5.3 UV View

The UV View in the center of the main window enables inspection and editing directly on the flattened 2D UV layout.

- **Unified Navigation & Controls**: The same selection and paint tools operate identically in the UV View as in the 3D SceneView.
- **Canvas Navigation**:
  - Mouse Wheel: Zoom in and out
  - Middle Mouse Drag (or Alt + Left Drag): Pan canvas
  - Focus button (top-right) or `F` key: Reset zoom and center UV layout
- **Base Texture Overlay**:
  - Click the texture icon in the top right of the UV View to display the model's active texture in the background for visual alignment.
- **Importing Mask Images**:
  - Drag and drop any black-and-white texture file into the UV View to import dark areas directly into the paint layer.

---

### 5.4 Export and Material Application

Export textures from the main window's export panel or by pressing **`Ctrl + Enter`**.

#### Export Parameters
- **File Name**: Defaults to `{RendererName}_mask.png`. If a conflicting file name exists in the folder, duplicates are automatically avoided.
- **Resolution**: Select from 512, 1024, 2048, or 4096 pixels.
- **Channel Packing**:
  - Export as a standalone grayscale texture (Mask) or pack into a specific channel (R, G, B, or A) of an existing texture.
- **Advanced Settings (▶ Details)**:
  Click to expand (displays summary chips of current settings when collapsed).
  - **Invert**: Inverts black and white values.
  - **Padding (Dilation)**: Dilates mask boundaries outward by a specified number of pixels (0–32 px). This prevents mipmap filtering artifacts and seam bleeding at UV edges.
  - **Save Inverted Too**: Generates both normal and inverted versions in one operation.
  - **Save to Mask/ Subfolder**: Automatically creates a `Mask/` subfolder in the target texture directory and saves the exported mask inside it.

#### Material Assignment and Safe Overwriting
- Re-exporting an existing target overwrites the previous texture file. Because materials reference this file on disk, the material automatically updates in the scene without manual reassignment.
- Clicking **`Apply`** in the completion toast automatically analyzes shader properties and assigns the file to the correct mask slot.

---

### 5.5 UV Wireframe Export

Export UV wireframe layout images for reference in external painting applications.

1. Click the **Camera icon button** in the top right of the UV View.
2. The "Export UV Layout" configuration dialog opens.
3. Configure settings:
   - **Resolution**: 512 to 4096
   - **Render Elements**: Toggle polygon lines, seam lines, selection filter, and island coloring
   - **Background**: Transparent background or base texture overlay
   - **Color Settings**: Custom colors for wireframes, seams, and fills
4. Click **`Export`** to save the PNG image into your project.

---

## 6. Shortcut Key Reference

| Hotkey | Category | Action |
| :--- | :--- | :--- |
| **`Tab`** | Mode | Switch between Selection Mode and Paint Mode |
| **`Left Click`** | Selection | Toggle selection on and off |
| **`Double Click`** | Selection | Select or deselect entire connected component |
| **`Left Drag`** | Selection / Paint | Continuous drag selection or brush painting |
| **`Shift` + Click / Drag** | Selection / Paint | Force Add to selection (Force paint in Paint Mode) |
| **`Ctrl` + Click / Drag** | Selection / Paint | Force Remove from selection (Instant Eraser in Paint Mode) |
| **`Ctrl` + Right-Drag** | Paint Settings | Interactively adjust brush radius under cursor |
| **`[` / `]`** | Paint Settings | Decrease / Increase brush size |
| **`Shift` + Mouse Wheel** | Selection Assist | Expand (Wheel Up) / Shrink (Wheel Down) selection |
| **`X`** | Selection Assist | Toggle Symmetry Editing (X-Mirror) |
| **`1` / `2` / `3`** | Granularity | Switch selection granularity (1: Island / 2: Part / 3: Face) |
| **`B`** | Tool Picker | Select Brush tool |
| **`M`** | Tool Picker | Select Rectangle Fill tool |
| **`L`** | Tool Picker | Select Lasso Fill tool |
| **`Shift + B`** | Tool Picker | Select Eraser tool |
| **`C` (Hold)** | Interface | Summon Radial Menu under cursor |
| **`Space` (Hold)** | Control Assist | Suspend tool input (use default camera controls) |
| **`Ctrl + A`** | Selection Action | Select all polygons |
| **`Ctrl + I`** | Selection Action | Invert selection |
| **`Ctrl + Z`** | History | Undo previous action |
| **`Ctrl + Y`** | History | Redo undone action |
| **`Ctrl + Enter`** | Export | Quick export mask texture |
| **`Esc`** | Session | Finish editing session (Closes Radial Menu if open) |
| **`F`** | View Navigation | Center and reset UV View zoom |

---

## 7. Notes and Troubleshooting

### Frequently Asked Questions

#### Q. Applying the exported mask does not change the model appearance in the scene.
- **A.** Check the following:
  1. Ensure that the corresponding shader feature utilizing masks (e.g., lilToon's "Outfit Mask", "Alpha Mask", or "Rimlight Mask") is enabled on the material.
  2. If the mesh contains multiple material slots, verify that the mask was applied to the intended submesh material.
  3. Verify that the texture import settings (such as `Alpha Source` or `sRGB (Color Texture)`) match shader requirements (the automatic `Apply` action sets recommended values automatically).

#### Q. Dark or light fringes (bleeding) appear along UV seams in the distance.
- **A.** This is typically caused by mipmap downsampling blending the mask edge with background pixels. In the main window, open **Advanced Settings > Padding** and increase the padding value (e.g., 8–16 px) before re-exporting. Dilating the boundary colors outward eliminates seam bleeding.

#### Q. Symmetry Editing (X key) does not affect the opposite side.
- **A.** Consider the following factors:
  1. Symmetry Editing detects pairs relative to the mesh's local coordinate center (X = 0). If the model's pivot is offset from its visual centerline, symmetric pairs cannot be identified.
  2. Asymmetrical mesh geometry or mismatched UV mapping between left and right sides cannot be mirrored automatically. In this scenario, disable Symmetry Editing and select the regions individually.

#### Q. What happens if Unity is closed unexpectedly or scripts recompile during editing?
- **A.** Edit states (selection buffers and paint layers) are automatically cached to persistent disk storage. When reopening the project or returning after script compilation, selecting the same target object restores your work session seamlessly.

#### Q. Can I undo an accidental selection clear?
- **A.** Yes. Press **`Ctrl + Z`** to revert. All actions—including selections, brush strokes, and clearing—are recorded in a unified undo stack.
