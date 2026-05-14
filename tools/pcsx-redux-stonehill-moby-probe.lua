-- PCSX-Redux live Stone Hill moby probe.
--
-- Load this in PCSX-Redux's Lua console while Spyro 1 is running in Stone Hill.
-- It reads the same _ptr_levelMobys table used by the editor's RAM importer.
-- Select the editor L# index, apply a large coordinate offset, and watch which
-- visible object moves. Revert before saving or continuing normal play.

local ffi = require("ffi")
local bit = require("bit")

local MOBY_POINTER_ADDR = 0x80075828
local LEVEL_ID_ADDR = 0x800758B4
local MOBY_STRIDE = 0x50
local MAX_MOBYS = 512
local COORD_SCALE = 16

local probe = {
  index = 109,
  deltaX = 0,
  deltaY = 0,
  deltaZ = 1000,
  saved = {},
  selectedOnly = true,
}

local function ramOffset(address)
  return bit.band(address, 0x1fffff)
end

local function ptrU8(mem, address)
  return mem + ramOffset(address)
end

local function readU32(mem, address)
  return ffi.cast("uint32_t*", ptrU8(mem, address))[0]
end

local function readI32(mem, address)
  return ffi.cast("int32_t*", ptrU8(mem, address))[0]
end

local function writeI32(mem, address, value)
  ffi.cast("int32_t*", ptrU8(mem, address))[0] = value
end

local function readU8(mem, address)
  return ptrU8(mem, address)[0]
end

local function isMainRamPointer(pointer)
  return pointer >= 0x80000000 and pointer < 0x80200000
end

local function mobyAddress(mem, index)
  local tablePtr = readU32(mem, MOBY_POINTER_ADDR)
  if not isMainRamPointer(tablePtr) then return nil, tablePtr end
  return tablePtr + (index * MOBY_STRIDE), tablePtr
end

local function readMoby(mem, index)
  local address, tablePtr = mobyAddress(mem, index)
  if not address then return nil, tablePtr end
  local typeId = readU8(mem, address + 0x48)
  local state = readU8(mem, address + 0x49)
  if typeId == 0 or typeId > 0x7f or state > 0x7f then
    return nil, tablePtr
  end
  return {
    address = address,
    tablePtr = tablePtr,
    typeId = typeId,
    state = state,
    rawX = readI32(mem, address + 4),
    rawY = readI32(mem, address + 8),
    rawZ = readI32(mem, address + 12),
  }, tablePtr
end

local function keyFor(address)
  return string.format("%08X", address)
end

local function saveOriginal(record)
  local key = keyFor(record.address)
  if probe.saved[key] then return probe.saved[key] end
  probe.saved[key] = {
    address = record.address,
    rawX = record.rawX,
    rawY = record.rawY,
    rawZ = record.rawZ,
  }
  return probe.saved[key]
end

local function applyDelta(mem, record)
  local original = saveOriginal(record)
  writeI32(mem, record.address + 4, original.rawX + (probe.deltaX * COORD_SCALE))
  writeI32(mem, record.address + 8, original.rawY + (probe.deltaY * COORD_SCALE))
  writeI32(mem, record.address + 12, original.rawZ + (probe.deltaZ * COORD_SCALE))
end

local function revertRecord(mem, record)
  local original = probe.saved[keyFor(record.address)]
  if not original then return end
  writeI32(mem, record.address + 4, original.rawX)
  writeI32(mem, record.address + 8, original.rawY)
  writeI32(mem, record.address + 12, original.rawZ)
end

local function revertAll(mem)
  for _, original in pairs(probe.saved) do
    writeI32(mem, original.address + 4, original.rawX)
    writeI32(mem, original.address + 8, original.rawY)
    writeI32(mem, original.address + 12, original.rawZ)
  end
end

local function formatCoord(raw)
  return string.format("%d (%.2f editor units)", raw, raw / COORD_SCALE)
end

local function renderNearby(mem, selected)
  if not selected then return end
  imgui.Text("Nearby plausible mobys")
  local shown = 0
  for i = 0, MAX_MOBYS - 1 do
    local record = readMoby(mem, i)
    if record then
      local dx = (record.rawX - selected.rawX) / COORD_SCALE
      local dy = (record.rawY - selected.rawY) / COORD_SCALE
      local dz = (record.rawZ - selected.rawZ) / COORD_SCALE
      local dist2 = dx * dx + dy * dy
      if dist2 <= (1600 * 1600) and math.abs(dz) <= 1000 then
        imgui.Text(string.format("L%d type 0x%02X state 0x%02X dx %.0f dy %.0f dz %.0f", i, record.typeId, record.state, dx, dy, dz))
        shown = shown + 1
        if shown >= 16 then break end
      end
    end
  end
  if shown == 0 then imgui.Text("No nearby plausible mobys found.") end
end

function DrawImguiFrame()
  imgui.safe.Begin("Stone Hill moby probe", function()
    local mem = PCSX.getMemPtr()
    local levelId = readU32(mem, LEVEL_ID_ADDR)
    local selected, tablePtr = readMoby(mem, probe.index)

    imgui.Text(string.format("Level id: 0x%08X", levelId))
    imgui.Text(string.format("_ptr_levelMobys: 0x%08X", tablePtr or 0))
    _, probe.index = imgui.SliderInt("Editor L# / moby index", probe.index, 0, MAX_MOBYS - 1)
    _, probe.deltaX = imgui.SliderInt("X delta (editor units)", probe.deltaX, -2000, 2000)
    _, probe.deltaY = imgui.SliderInt("Y delta (editor units)", probe.deltaY, -2000, 2000)
    _, probe.deltaZ = imgui.SliderInt("Z delta (editor units)", probe.deltaZ, -2000, 2000)

    if selected then
      imgui.Text(string.format("Selected L%d at 0x%08X type 0x%02X state 0x%02X", probe.index, selected.address, selected.typeId, selected.state))
      imgui.Text("Raw X: " .. formatCoord(selected.rawX))
      imgui.Text("Raw Y: " .. formatCoord(selected.rawY))
      imgui.Text("Raw Z: " .. formatCoord(selected.rawZ))

      if imgui.Button("Apply delta to selected") then
        applyDelta(mem, selected)
      end
      imgui.SameLine()
      if imgui.Button("Revert selected") then
        revertRecord(mem, selected)
      end
      imgui.SameLine()
      if imgui.Button("Revert all") then
        revertAll(mem)
      end

      renderNearby(mem, selected)
    else
      imgui.Text("Selected record is not a plausible live moby.")
      if isMainRamPointer(tablePtr or 0) == false then
        imgui.Text("Load into a level first; _ptr_levelMobys is not valid yet.")
      end
    end
  end)
end
