# UV Mask Maker V4 User Manual (English)

This is the user manual for **UV Mask Maker V4**, an intuitive Unity editor extension designed to create, edit, and apply mask textures directly on 3D models in Unity.

---

## 1. Tool Overview

**UV Mask Maker V4** is a Unity editor extension that allows you to select polygons/UVs and paint directly on 3D meshes (`SkinnedMeshRenderer` / `MeshRenderer`)—such as VRChat avatars and costumes—within the Unity SceneView to generate, export, and apply black-and-white mask textures.

Without round-tripping between external painting applications (Photoshop, Clip Studio Paint, etc.) and Unity, you can preview changes directly on the model in Unity and generate/assign masks in just a few simple steps.

### Key Highlights

- **Zero-Travel Workflow**: No need to move your mouse back and forth to screen edges. Operations are completed under your hand via the Radial Menu under the cursor (`Hold C`) and in-place brush resizing (`Ctrl + Right-Drag`).
- **Smart Toggle & Intuitive Selection**: No separate "Add" and "Remove" modes. Clicking an unselected region adds it, while clicking a selected region removes it. Double-clicking instantly selects an entire connected garment piece or accessory.
- **Symmetry Editing (X-Mirror)**: Automatically identifies symmetrical mesh counterparts across the X-axis. Selections and brush strokes on arms, legs, or shoes are instantly mirrored to the other side, cutting operations in half.
- **One-Click Start & Auto-Recovery**: Selecting an object and clicking "Start" automatically handles mesh analysis, isolation, camera framing, and session recovery.
- **Quick Export & Smart Apply**: Press `Ctrl + Enter` to export instantly. Click the **[Apply]** button on the toast notification to assign the generated mask directly into shader mask slots (lilToon, Poiyomi, etc.).
- **UV Wireframe Export**: Export clean UV wireframe PNGs with one click to use as reference backdrops in external painting software.

---

## 2. Quick Start (Minimal Usage)

Here is the quickest workflow to select part of a costume, export a mask, and apply it to a material:

```
[Select in Hierarchy] → [Right-Click to Start] → [Click Mesh to Select] → [Ctrl+Enter to Export] → [Click [Apply]]
```

### Step 1: Start Editing
1. In the **Hierarchy**, select the object (mesh of an avatar or outfit) you want to create a mask for.
2. Right-click and choose **`dennokoworks` > `MaskMaker V4`**.
   - *(Alternatively, open the window from the top menu `dennokoworks` > `MaskMaker V4` and click **"Start"**)*.
3. The target mesh is automatically isolated, and the SceneView camera frames it.

### Step 2: Select Regions to Mask
1. In the **SceneView**, **Left-Click** the part you want to mask.
   - The clicked UV island is selected and displayed with a mask overlay.
   - Clicking an already selected island unselects it.
   - **Double-Click** to select the entire connected part (e.g., ribbon, shoe, button).
2. For symmetrical parts like sleeves or gloves, press **`X`** to turn on X-Mirror. Clicking one side will automatically select the matching opposite side.

### Step 3: Export and Apply to Material
1. Press **`Ctrl + Enter`** on your keyboard (or click the "Export" button on the UI).
2. Once exported, a toast notification appears beside your cursor: **`[SaveAs] (FileName).png  [Apply]`**.
3. Click the **`[Apply]`** button inside the toast.
   - The texture is automatically assigned to the material's mask slot (e.g., lilToon's 2nd Color Mask or Poiyomi's Decal Mask).
   - If multiple mask slots are available, a quick candidate list appears for you to pick the desired slot.

---

## 3. Feature List

| Category | Feature | Description |
| :--- | :--- | :--- |
| **Selection System** | **Smart Toggle** | Click unselected to add, click selected to remove (no mode switching needed) |
| | **Stroke Selection** | Left-drag to continuously select or deselect multiple islands |
| | **Part Selection** | Double-click to select an entire physically connected mesh component |
| | **Selection Granularity** | Switch between "Island" (UV island), "Part" (connected mesh), and "Face" (triangle polygon) |
| | **Grow / Shrink** | `Shift + Mouse Wheel` expands or shrinks selection to adjacent polygons |
| | **Symmetry (X-Mirror)** | Press `X` to toggle. Automatically mirrors selections across the X-axis |
| | **Select All / Invert / Clear** | Standard shortcuts: `Ctrl + A` (Select All), `Ctrl + I` (Invert), and Clear |
| | **Hover Outline Preview** | White outline previews addition; red outline previews removal before clicking |
| **Painting System** | **4 Paint Tools** | Brush, Rectangle fill, Lasso fill, and Eraser |
| | **In-Place Brush Resizing** | `Ctrl + Right-Drag` horizontally to smoothly adjust brush radius under the cursor |
| | **Instant Eraser** | Hold `Ctrl` while painting with any tool to temporarily erase |
| | **Ultra-HD Internal Buffer** | Hand-painted strokes are stored internally at 4096×4096 px with zero quality degradation |
| **View & Preview** | **SceneView Overlay** | Real-time mask projection and UV seam edge visualization on 3D meshes |
| | **2D UV View** | Integrated 2D UV layout editor supporting direct clicking, dragging, and painting |
| | **Base Texture Overlay** | Display the model's base texture underneath the UV view for accurate alignment |
| | **Target Isolation** | Temporarily hides unrelated scene objects while working |
| | **X-Ray View** | View and select through occluded backfaces and inner geometry |
| | **Pause Picking** | Hold `Space` to pause tool picking and use normal Unity camera/transform tools |
| **Export & Apply** | **B&W Mask PNG Export** | Exports linear/non-sRGB PNG with appropriate mipmap settings |
| | **Padding / Dilation** | Automatically dilates mask edges into empty UV space to prevent mipmap seam bleeding |
| | **Inverted Mask Export** | Export an inverted mask or generate both normal and inverted masks simultaneously |
| | **Channel Packing** | Inject the mask into a specific RGBA channel of an existing texture |
| | **In-Place Overwrite** | Re-exporting overwrites the previous file, keeping existing material references intact |
| | **Smart Material Apply** | 1-click assignment to shader mask slots directly from the export toast |
| | **UV Wireframe Export** | Export UV wireframe images as reference templates for external paint programs |
| | **Import Mask Image** | Drag and drop an existing mask image into the UV View to import it into the paint layer |
| **UI & Workflow** | **Radial Pie Menu** | Hold `C` to summon a circular tool menu directly beneath the cursor |
| | **Floating Capsule Toolbar** | Compact, dockable toolbar floating inside the SceneView |
| | **Unified Undo / Redo** | Selection, painting, and material application share a unified history stack (`Ctrl+Z` / `Ctrl+Y`) |
| | **Auto-Save & Restore** | Automatically saves edit states; work persists across recompilations and Unity restarts |

---

## 4. Detailed Guide

### 4.1 Selection Mode in Detail

Selection mode (switched with `Tab`) is the primary mode for masking regions along the polygon structure of the mesh.

#### 3 Selection Granularities
Switch using keys `1`, `2`, `3` or via the Radial Menu / Capsule Toolbar:
- **1: Island (UV Island)** *(Default)*: Selects by UV island boundary. Ideal for selecting specific fabric patterns, sleeves, or accessory sections.
- **2: Part (Connected Mesh)**: Selects the entire connected geometric component sharing vertices (equivalent to double-clicking).
- **3: Face (Polygon Triangle)**: Selects individual triangle polygons. Ideal for trimming or carving out tiny sections from an island.

> [!TIP]
> **Switching granularity preserves existing selections**  
> Fine selections made in "Face" mode are not lost when switching to "Island" mode. The granularity setting only dictates *the size of the unit for your next click*.

#### Mouse & Keyboard Techniques
- **Smart Toggle**: Clicking unselected geometry adds it; clicking selected geometry removes it.
- **Stroke Selection (Left-Drag)**: Click and drag across multiple islands to continuously select or deselect them in one fluid motion.
- **Forced Add / Forced Remove**:
  - `Shift + Click / Drag`: Forces **addition**, regardless of whether the target was already selected.
  - `Ctrl + Click / Drag`: Forces **removal / deselection**.
- **Grow / Shrink Selection**:
  - `Shift + Mouse Wheel Up`: Expands the selection boundary outward by one adjacent polygon step.
  - `Shift + Mouse Wheel Down`: Shrinks the selection boundary inward by one step.

#### X-Mirror (Symmetry)
- Press **`X`** to toggle symmetry.
- When enabled, the corresponding geometry across the avatar's center plane (X=0) is highlighted and updated simultaneously.
- Cuts camera navigation and click counts in half when working on symmetrical clothing, sleeves, gloves, or shoes.

---

### 4.2 Paint Mode in Detail

Press **`Tab`** to enter Paint Mode. Use this when you want freehand control across or within polygons.

#### 4 Paint Tools
Quickly switch tools using the Radial Menu (`Hold C`) or hotkeys:
1. **Brush (`B`)**: Projects a circular brush onto the 3D surface.
2. **Rectangle (`M`)**: Fills a rectangular screen-space region in one drag.
3. **Lasso (`L`)**: Fills a freehand lasso region.
4. **Eraser (`Shift + B`)**: Erases painted mask strokes.

#### In-Place Brush Resizing (`Ctrl + Right-Drag`)
- Hold **`Ctrl` and drag the Right Mouse Button horizontally** over the mesh to smoothly resize the brush circle right under your cursor.
- Eliminates mouse travel to top sliders.
- You can also tap `[` (decrease) and `]` (increase).

#### Instant Eraser Modifier
- In any paint tool, holding **`Ctrl` while painting acts as an eraser**. You do not need to switch tools back and forth.

#### Ultra-HD Internal Buffer
- All hand-painted strokes are preserved internally at **4096×4096 px**.
- Changing the export resolution preview to 512 or 1024 will display the resulting pixelated look in real-time, but does not degrade the underlying stroke data. You can return to 2048 or 4096 at any time with crisp fidelity.

---

### 4.3 Working with the 2D UV View

The **UV View** in the central section of the tool tab allows working directly on the unwrapped 2D UV layout.

- **Unified Interaction**: The same selection tools, brush tools, and hotkeys function identically on the 2D UV canvas as in the 3D SceneView.
- **Navigation Controls**:
  - Mouse Wheel: Zoom in / Zoom out
  - Middle Mouse Drag: Pan across canvas
  - Frame Button (top-right zoom icon or `F` key): Reset view to center and 100% zoom
- **Base Texture Overlay**:
  - Click the texture icon in the UV View's top-right corner to display the model's base texture as a semi-transparent backdrop, making it easy to align masks with existing texture art.
- **Importing Existing Masks**:
  - **Drag and drop** any black-and-white texture file from your Project window into the UV View. Dark areas will be imported directly into the hand-painted mask layer (Undo supported).

---

### 4.4 Export Settings & Material Application

When ready, click "Export" or press **`Ctrl + Enter`** to write out the mask texture.

#### Primary Export Options
- **File Name**: Defaults to `{RendererName}_mask.png`.
- **Resolution**: Choose from `512`, `1024`, `2048`, or `4096` (automatically detected from the base texture).
- **Channel Packing (Optional)**:
  - When checked, writes the mask specifically into the chosen channel (R, G, B, or A) of an existing texture (Base PNG).
  - Perfect for metallic, smoothness, or ambient occlusion composite maps.

#### Advanced Options (Expand ▸ Details)
- **Invert Mask**: Inverts black and white values.
- **Pixel Margin (Padding)**: Dilates mask boundaries outward into unused UV space. This prevents dark or discolored mipmap bleeding along UV seams in-game.
- **Save Inverted Too**: Generates both the standard mask (`_mask.png`) and the inverted mask (`_mask_inv.png`) simultaneously.

#### Overwriting & Smart Material Assignment
- **Safe Overwriting**: Re-exporting from the same target mesh overwrites the previously exported file without creating cluttering numbered files (`_mask 1.png`, etc.). Existing material links update automatically.
- **Smart Apply Button**:
  - The toast notification displaying **`[SaveAs] (FileName).png  [Apply]`** allows instant assignment to your shader.
  - It inspects popular avatar shaders (lilToon, Poiyomi, UTS2, Standard) and assigns the texture to the appropriate mask property (e.g., Decal Mask, 2nd Main Color Mask).
  - Selected slot preferences are remembered per mesh.

---

### 4.5 UV Wireframe Export

Export clean wireframe layout images for use as template layers in Clip Studio Paint, Photoshop, or other external software.

1. Click the **`[Camera]` icon button** in the top right of the UV View.
2. The "UV Layout Export" dialog will appear.
3. Configure the output:
   - **Resolution**: 512 to 4096 (Default: 2048).
   - **Elements**:
     - Polygon Wireframe (internal triangle edges).
     - Seam Lines (outer UV boundary edges).
     - Selection Only (renders only currently selected islands/polygons).
     - Island Coloring (fills each UV island with a distinct pastel color).
   - **Background**: Transparent or Base Texture.
   - **Colors**: Customize wireframe, seam, and island fill colors.
4. Click **`[Export]`** to save `{FileName}_uv.png` into your project.

---

### 4.6 Interface & Shortcut Reference

#### UI Layout
1. **Tool Tab (EditorWindow)**:
   - Opened via `dennokoworks` > `MaskMaker V4`.
   - Contains Target Mesh picker, UV View, Export settings, and the primary action button.
2. **Floating Capsule Toolbar**:
   - A rounded floating bar appearing inside the SceneView during editing. Drag to reposition anywhere.
   - Houses Mode toggle, Granularity, Paint Tools, X-Mirror, Isolation, Undo/Redo, Export, and Selection counter.
3. **Cursor Radial Menu**:
   - Hold **`C`** anywhere in the SceneView or UV View to open a circular menu centered directly under the cursor.
   - Flick the cursor in the direction of the desired tool and release `C` to select instantly.

#### Shortcut Reference Table

| Key | Action |
| :--- | :--- |
| **`Tab`** | Toggle Selection Mode ⇄ Paint Mode |
| **`Left Click`** | Smart Toggle (Select if unselected, Deselect if selected) |
| **`Double Click`** | Select / Deselect entire connected component |
| **`Left Drag`** | Continuous stroke selection / painting |
| **`Shift` + Click / Drag** | Force Add (Force Paint in Paint Mode) |
| **`Ctrl` + Click / Drag** | Force Remove (Temporary Eraser in Paint Mode) |
| **`Ctrl` + Right-Drag** | In-place smooth brush resizing |
| **`Shift` + Mouse Wheel** | Grow (Up) / Shrink (Down) selection |
| **`X`** | Toggle Symmetry (X-Mirror) |
| **`1` / `2` / `3`** | Selection Granularity (1: Island / 2: Part / 3: Face) |
| **`B` / `M` / `L` / `Shift+B`** | Paint Tools (B: Brush / M: Rect / L: Lasso / Shift+B: Eraser) |
| **`[` / `]`** | Decrease / Increase brush size |
| **`C` (Hold)** | Open Radial Menu under cursor |
| **`Space` (Hold)** | Pause picking (enables standard Unity camera and transform tools) |
| **`Ctrl + A`** | Select All |
| **`Ctrl + I`** | Invert Selection |
| **`Ctrl + Z` / `Ctrl + Y`** | Unified Undo / Redo |
| **`Ctrl + Enter`** | Quick Export with current settings |
| **`F`** | Reset UV View zoom and pan to center |
