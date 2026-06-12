using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ImGuiNET;
using UnityModManagerNet;


using Vector2 = System.Numerics.Vector2;
using Vector4 = System.Numerics.Vector4;
using Object = UnityEngine.Object;
namespace CheryTools;

public static class FreeMakeEditor
{
	public static bool IsOpen = false;

	private static int _editMode = 0;

	private static List<KVNode> _selectedNodes = new List<KVNode>();

	private static bool _isDraggingCanvas = false;

	private static Vector2 _canvasScroll = new Vector2(0f, 0f);

	private static float _canvasZoom = 1f;

	private static bool _isDraggingMarquee = false;

	private static Vector2 _marqueeStart;

	private static bool _isDraggingNodes = false;

	private static Dictionary<KVNode, Vector2> _dragStartPositions = new Dictionary<KVNode, Vector2>();
	private static readonly List<KVNode> _drawNodesBuffer = new List<KVNode>();
	private static readonly List<KVNode> _hitNodesBuffer = new List<KVNode>();
	private static float _lastCanvasClickTime = -10f;
	private static Vector2 _lastCanvasClickPos = new Vector2(float.MinValue, float.MinValue);
	private static bool _dragMoved = false;
	private const float CanvasDoubleClickTime = 0.35f;
	private const float CanvasDoubleClickDistance = 8f;
	private static readonly string KeyCornerRadiusLabel = new string(new char[] { '\u5706', '\u89d2', '\u534a', '\u5f84', '#', '#', 'f', 'm', '_', 'k', 'e', 'y', '_', 'c', 'o', 'r', 'n', 'e', 'r', '_', 'r', 'a', 'd', 'i', 'u', 's' });
	private static readonly string ImageCornerRadiusLabel = new string(new char[] { '\u5706', '\u89d2', '\u534a', '\u5f84', '#', '#', 'f', 'm', '_', 'i', 'm', 'a', 'g', 'e', '_', 'c', 'o', 'r', 'n', 'e', 'r', '_', 'r', 'a', 'd', 'i', 'u', 's' });

	private static KVNode _keyBindCaptureNode = null;
	private static string _keyBindCaptureId = string.Empty;
	private static int _keyBindCaptureStartFrame = -1;

	private static Vector2 _lastWindowPos = new Vector2(100f, 100f);
	private static Vector2 _lastWindowSize = new Vector2(900f, 600f);

	private struct AlignLine
	{
		public bool IsVertical;
		public float Coord;
		public float MinLimit;
		public float MaxLimit;
	}

	private static List<AlignLine> _activeAlignLines = new List<AlignLine>();
	private static float _dragTotalDeltaX = 0f;
	private static float _dragTotalDeltaY = 0f;

	private static bool DrawColorPicker(string label, float[] colorData)
	{
		Vector4 col = new Vector4(colorData[0], colorData[1], colorData[2], colorData[3]);
		if (ImGui.ColorEdit4(label, ref col, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaPreviewHalf))
		{
			colorData[0] = col.X;
			colorData[1] = col.Y;
			colorData[2] = col.Z;
			colorData[3] = col.W;
			return true;
		}
		return false;
	}

	private static void CopyColor(float[] source, ref float[] target)
	{
		if (source == null || source.Length < 4)
		{
			return;
		}
		if (target == null || target.Length != 4)
		{
			target = new float[4];
		}
		Array.Copy(source, target, 4);
	}

	private static bool ImportResourcePath(ref string path, string category, bool rebuildFonts)
	{
		string imported = CheryToolsAssets.ImportExternalAsset(path, category);
		if (string.Equals(path ?? string.Empty, imported ?? string.Empty, StringComparison.Ordinal))
		{
			return false;
		}

		path = imported;
		if (rebuildFonts)
		{
			ImGuiController.NeedsFontAtlasRebuild = true;
		}
		Main.RequestSave();
		return true;
	}

	private static void StopKeyBindCapture()
	{
		_keyBindCaptureNode = null;
		_keyBindCaptureId = string.Empty;
		_keyBindCaptureStartFrame = -1;
	}

	private static bool IsKeyboardKey(UnityEngine.KeyCode key)
	{
		if (key == UnityEngine.KeyCode.None)
		{
			return false;
		}

		string name = key.ToString();
		return !name.StartsWith("Mouse", StringComparison.Ordinal) && !name.StartsWith("Joystick", StringComparison.Ordinal);
	}

	private static bool TryGetPressedKeyboardKey(out UnityEngine.KeyCode pressedKey)
	{
		pressedKey = UnityEngine.KeyCode.None;
		if (!UnityEngine.Input.anyKeyDown)
		{
			return false;
		}

		Array values = Enum.GetValues(typeof(UnityEngine.KeyCode));
		for (int i = 0; i < values.Length; i++)
		{
			UnityEngine.KeyCode key = (UnityEngine.KeyCode)values.GetValue(i);
			if (!IsKeyboardKey(key))
			{
				continue;
			}

			if (UnityEngine.Input.GetKeyDown(key))
			{
				pressedKey = key;
				return true;
			}
		}

		return false;
	}

	private static bool DrawKeyBindCapture(KVNode node, string label, string id)
	{
		if (node == null)
		{
			return false;
		}

		string keyBind = string.IsNullOrEmpty(node.KeyBind) ? "None" : node.KeyBind;
		string displayName = KeyDisplayNames.GetKeySymbol(keyBind);
		bool isCapturing = ReferenceEquals(_keyBindCaptureNode, node) && string.Equals(_keyBindCaptureId, id, StringComparison.Ordinal);

		ImGui.Text(label + ": " + displayName + " (" + keyBind + ")");
		if (ImGui.Button(isCapturing ? "等待按键...##" + id : "点击绑定##" + id, new Vector2(120f, 0f)))
		{
			_keyBindCaptureNode = node;
			_keyBindCaptureId = id;
			_keyBindCaptureStartFrame = UnityEngine.Time.frameCount;
			isCapturing = true;
		}

		ImGui.SameLine();
		if (ImGui.Button("清空##" + id, new Vector2(70f, 0f)))
		{
			node.KeyBind = "None";
			node.CustomText = "";
			StopKeyBindCapture();
			InputInterceptor.UpdateAllowedKeys();
			Main.RequestSave();
			return true;
		}

		if (!isCapturing)
		{
			return false;
		}

		ImGui.SameLine();
		ImGui.TextColored(new Vector4(1f, 0.85f, 0.35f, 1f), "按下键盘按键，Esc 取消");
		if (UnityEngine.Time.frameCount <= _keyBindCaptureStartFrame)
		{
			return false;
		}

		if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Escape))
		{
			StopKeyBindCapture();
			return false;
		}

		if (!TryGetPressedKeyboardKey(out UnityEngine.KeyCode pressedKey))
		{
			return false;
		}

		node.KeyBind = pressedKey.ToString();
		node.CustomText = KeyDisplayNames.GetKeySymbol(node.KeyBind);
		StopKeyBindCapture();
		InputInterceptor.UpdateAllowedKeys();
		Main.RequestSave();
		return true;
	}

	private static void BuildDrawNodes(List<KVNode> source)
	{
		_drawNodesBuffer.Clear();
		if (source == null)
		{
			return;
		}

		for (int i = 0; i < source.Count; i++)
		{
			KVNode node = source[i];
			if (node != null && node.NodeType == 3)
			{
				_drawNodesBuffer.Add(node);
			}
		}
		for (int i = 0; i < source.Count; i++)
		{
			KVNode node = source[i];
			if (node != null && node.NodeType != 3)
			{
				_drawNodesBuffer.Add(node);
			}
		}
	}

	private static bool IsNodeSelectableInCurrentMode(KVNode node, ImGuiIOPtr io)
	{
		if (node == null)
		{
			return false;
		}

		if (_editMode == 2)
		{
			return node.NodeType == 3;
		}

		return node.NodeType != 3 && (!node.IsUnselectable || io.KeyShift);
	}

	private static bool IsMouseInsideNode(Vector2 mousePos, Vector2 min, Vector2 max)
	{
		return mousePos.X >= min.X && mousePos.X <= max.X && mousePos.Y >= min.Y && mousePos.Y <= max.Y;
	}

	private static float ClampNodeCornerRadius(float value)
	{
		if (float.IsNaN(value) || float.IsInfinity(value))
		{
			return -1f;
		}
		return Math.Max(-1f, Math.Min(256f, value));
	}

	private static float ResolveNodeCornerRadiusForCanvas(KVNode node, bool isImageNode, float keyViewerScale, float canvasZoom, float width, float height)
	{
		float radius = 0f;
		if (node != null && !float.IsNaN(node.CornerRadius) && !float.IsInfinity(node.CornerRadius) && node.CornerRadius >= 0f)
		{
			radius = node.CornerRadius * keyViewerScale * canvasZoom;
		}
		else if (!isImageNode)
		{
			radius = (float)Math.Floor(6f * keyViewerScale) * canvasZoom;
		}

		return Math.Max(0f, Math.Min(radius, Math.Min(width, height) * 0.5f));
	}

	private static bool ConsumeCanvasDoubleClick(Vector2 mousePos)
	{
		float now = UnityEngine.Time.unscaledTime;
		float dx = mousePos.X - _lastCanvasClickPos.X;
		float dy = mousePos.Y - _lastCanvasClickPos.Y;
		bool isDoubleClick = now - _lastCanvasClickTime <= CanvasDoubleClickTime && dx * dx + dy * dy <= CanvasDoubleClickDistance * CanvasDoubleClickDistance;
		_lastCanvasClickTime = now;
		_lastCanvasClickPos = mousePos;
		return isDoubleClick;
	}

	private static KVNode GetSelectedHitNode(List<KVNode> hitNodes)
	{
		if (hitNodes == null || hitNodes.Count == 0 || _selectedNodes.Count == 0)
		{
			return null;
		}

		for (int i = 0; i < hitNodes.Count; i++)
		{
			KVNode node = hitNodes[i];
			if (_selectedNodes.Contains(node))
			{
				return node;
			}
		}
		return null;
	}

	private static KVNode PickNodeForCanvasClick(List<KVNode> hitNodes, bool isDoubleClick, bool ctrlHeld)
	{
		if (hitNodes == null || hitNodes.Count == 0)
		{
			return null;
		}

		if (isDoubleClick && hitNodes.Count > 1)
		{
			KVNode selectedHit = GetSelectedHitNode(hitNodes);
			int index = selectedHit != null ? hitNodes.IndexOf(selectedHit) : -1;
			return hitNodes[(index + 1 + hitNodes.Count) % hitNodes.Count];
		}

		if (!ctrlHeld)
		{
			KVNode selectedHit = GetSelectedHitNode(hitNodes);
			if (selectedHit != null)
			{
				return selectedHit;
			}
		}

		return hitNodes[0];
	}

	private static void ApplyCanvasClickSelection(KVNode target, bool isDoubleClick, bool ctrlHeld)
	{
		if (target == null)
		{
			return;
		}

		if (isDoubleClick)
		{
			_selectedNodes.Clear();
			_selectedNodes.Add(target);
			return;
		}

		if (ctrlHeld)
		{
			if (_selectedNodes.Contains(target))
			{
				_selectedNodes.Remove(target);
			}
			else
			{
				_selectedNodes.Add(target);
			}
			return;
		}

		if (!_selectedNodes.Contains(target))
		{
			_selectedNodes.Clear();
			_selectedNodes.Add(target);
		}
	}

	private static void BeginNodeDrag()
	{
		if (_selectedNodes.Count == 0)
		{
			return;
		}

		_isDraggingNodes = true;
		_isDraggingMarquee = false;
		_dragStartPositions.Clear();
		foreach (KVNode selectedNode in _selectedNodes)
		{
			_dragStartPositions[selectedNode] = new Vector2(selectedNode.PositionX, selectedNode.PositionY);
		}
		_dragTotalDeltaX = 0f;
		_dragTotalDeltaY = 0f;
		_dragMoved = false;
		_activeAlignLines.Clear();
	}

	public static void Draw()
	{
		if (!IsOpen)
		{
			return;
		}

		ImGuiWindowFlags flags = ImGuiWindowFlags.None;
		Vector2 currentMousePos = ImGui.GetIO().MousePos;
		float titleBarHeight = ImGui.GetFrameHeight();
		bool mouseOnTitleBar = currentMousePos.X >= _lastWindowPos.X && currentMousePos.X <= _lastWindowPos.X + _lastWindowSize.X &&
		                       currentMousePos.Y >= _lastWindowPos.Y && currentMousePos.Y <= _lastWindowPos.Y + titleBarHeight;

		if (!mouseOnTitleBar || _isDraggingNodes || _isDraggingMarquee)
		{
			flags |= ImGuiWindowFlags.NoMove;
		}

		ImGui.SetNextWindowSize(_lastWindowSize, ImGuiCond.FirstUseEver);
		if (ImGui.Begin("FreeMake Editor", ref IsOpen, flags))
		{
			_lastWindowPos = ImGui.GetWindowPos();
			_lastWindowSize = ImGui.GetWindowSize();

			List<KVNode> list = KeyViewerManager.Instance?.GetEditingNodes();
			if (list == null)
			{
				ImGui.Text("没有选中的 KV 配置");
				ImGui.End();
				return;
			}
			KVConfiguration editingConfig = Main.Settings?.GetSelectedKeyViewerConfiguration();
			_selectedNodes.RemoveAll(node => !list.Contains(node));
			if (_keyBindCaptureNode != null && !list.Contains(_keyBindCaptureNode))
			{
				StopKeyBindCapture();
			}
			ImGui.BeginChild("FM_Sidebar", new Vector2(120f, 0f), ImGuiChildFlags.Borders);
			if (ImGui.Selectable("按键模式", _editMode == 0))
			{
				_editMode = 0;
			}
			if (ImGui.Selectable("文本模式", _editMode == 1))
			{
				_editMode = 1;
			}
			if (ImGui.Selectable("图片模式", _editMode == 2))
			{
				_editMode = 2;
			}
			ImGui.Spacing();
			ImGui.Separator();
			ImGui.Spacing();
			if (ImGui.Button("全选"))
			{
				_selectedNodes.Clear();
				_selectedNodes.AddRange(list);
			}
			if (ImGui.Button("取消选择"))
			{
				_selectedNodes.Clear();
			}
			if (ImGui.Button("添加新节点"))
			{
				list.Add(new KVNode
				{
					NodeType = _editMode == 2 ? 3 : 0,
					Width = 60f,
					Height = 60f,
					PositionX = 0f,
					PositionY = 0f,
					Scale = 1f,
					TextScale = 1f,
					CountScale = 1f
				});
				InputInterceptor.UpdateAllowedKeys();
				Main.RequestSave();
			}
			if (ImGui.Button("删除选中节点"))
			{
				foreach (KVNode selectedNode in _selectedNodes)
				{
					list.Remove(selectedNode);
				}
				_selectedNodes.Clear();
				InputInterceptor.UpdateAllowedKeys();
				Main.RequestSave();
			}
			ImGui.EndChild();
			ImGui.SameLine();
			ImGui.BeginChild("FM_Canvas", new Vector2(-300f, 0f), ImGuiChildFlags.Borders);
			Vector2 cursorScreenPos = ImGui.GetCursorScreenPos();
			Vector2 contentRegionAvail = ImGui.GetContentRegionAvail();
			ImDrawListPtr windowDrawList = ImGui.GetWindowDrawList();

			ImGuiIOPtr iO = ImGui.GetIO();
			bool flag = ImGui.IsWindowHovered();

			float centerX = cursorScreenPos.X + contentRegionAvail.X * 0.5f;
			float centerY = cursorScreenPos.Y + contentRegionAvail.Y * 0.5f;
			Vector2 canvasCenter = new Vector2(centerX + _canvasScroll.X, centerY + _canvasScroll.Y);

			if (flag && iO.MouseWheel != 0f)
			{
				float oldZoom = _canvasZoom;
				_canvasZoom = Math.Max(0.2f, Math.Min(3.0f, _canvasZoom + iO.MouseWheel * 0.05f));
				Vector2 mousePos = iO.MousePos;
				float mouseCanvasX = (mousePos.X - centerX - _canvasScroll.X) / oldZoom;
				float mouseCanvasY = (mousePos.Y - centerY - _canvasScroll.Y) / oldZoom;
				_canvasScroll.X = mousePos.X - centerX - mouseCanvasX * _canvasZoom;
				_canvasScroll.Y = mousePos.Y - centerY - mouseCanvasY * _canvasZoom;
				canvasCenter = new Vector2(centerX + _canvasScroll.X, centerY + _canvasScroll.Y);
			}
			if (flag && ImGui.IsMouseDown(ImGuiMouseButton.Middle))
			{
				_canvasScroll = new Vector2(_canvasScroll.X + iO.MouseDelta.X, _canvasScroll.Y + iO.MouseDelta.Y);
				canvasCenter = new Vector2(centerX + _canvasScroll.X, centerY + _canvasScroll.Y);
			}

			windowDrawList.AddRectFilled(cursorScreenPos, new Vector2(cursorScreenPos.X + contentRegionAvail.X, cursorScreenPos.Y + contentRegionAvail.Y), 4279900698u);

			float gridSpacing = 50f * _canvasZoom;
			float startX = (canvasCenter.X - cursorScreenPos.X) % gridSpacing;
			if (startX < 0f)
			{
				startX += gridSpacing;
			}
			for (float num = startX; num < contentRegionAvail.X; num += gridSpacing)
			{
				windowDrawList.AddLine(new Vector2(cursorScreenPos.X + num, cursorScreenPos.Y), new Vector2(cursorScreenPos.X + num, cursorScreenPos.Y + contentRegionAvail.Y), 4281545523u);
			}
			float startY = (canvasCenter.Y - cursorScreenPos.Y) % gridSpacing;
			if (startY < 0f)
			{
				startY += gridSpacing;
			}
			for (float num2 = startY; num2 < contentRegionAvail.Y; num2 += gridSpacing)
			{
				windowDrawList.AddLine(new Vector2(cursorScreenPos.X, cursorScreenPos.Y + num2), new Vector2(cursorScreenPos.X + contentRegionAvail.X, cursorScreenPos.Y + num2), 4281545523u);
			}

			Vector2 displaySize = ImGui.GetIO().DisplaySize;
			float halfW = displaySize.X * 0.5f * _canvasZoom;
			float halfH = displaySize.Y * 0.5f * _canvasZoom;
			Vector2 screenBoundsMin = new Vector2(canvasCenter.X - halfW, canvasCenter.Y - halfH);
			Vector2 screenBoundsMax = new Vector2(canvasCenter.X + halfW, canvasCenter.Y + halfH);
			windowDrawList.AddRect(screenBoundsMin, screenBoundsMax, 1728053247u, 0f, ImDrawFlags.None, 2f);
			string boundsLabel = $"屏幕边界 ({displaySize.X:F0} x {displaySize.Y:F0})";
			windowDrawList.AddText(ImGui.GetFont(), 14f * _canvasZoom, new Vector2(screenBoundsMin.X + 8f * _canvasZoom, screenBoundsMin.Y + 8f * _canvasZoom), 1728053247u, boundsLabel);
			float keyViewerScale = editingConfig != null ? editingConfig.Scale : Main.Settings.KeyViewerScale;
			float configBorderThickness = editingConfig != null ? editingConfig.BorderThickness : Main.Settings.KeyViewerBorderThickness;
			bool flag2 = false;
			_hitNodesBuffer.Clear();
			BuildDrawNodes(list);
			foreach (KVNode item in _drawNodesBuffer)
			{
				float num3 = keyViewerScale * item.Scale * _canvasZoom;
				float num4 = item.Width * num3;
				float num5 = item.Height * num3;
				float num6 = canvasCenter.X + item.PositionX * keyViewerScale * _canvasZoom;
				float num7 = canvasCenter.Y + item.PositionY * keyViewerScale * _canvasZoom;
				Vector2 p_min = new Vector2(num6, num7);
				Vector2 p_max = new Vector2(num6 + num4, num7 + num5);
				float previewCornerRadius = ResolveNodeCornerRadiusForCanvas(item, item.NodeType == 3, keyViewerScale, _canvasZoom, num4, num5);
				bool flag3 = _selectedNodes.Contains(item);
				uint col = (flag3 ? 4287120418u : 4282664004u);
				uint col2 = (flag3 ? 4294945331u : 4287137928u);
				if (item.UseCustomColor)
				{
					byte b = (byte)(item.ColorBgNormal[0] * 255f);
					byte b2 = (byte)(item.ColorBgNormal[1] * 255f);
					byte b3 = (byte)(item.ColorBgNormal[2] * 255f);
					uint num8 = (uint)(((byte)(item.ColorBgNormal[3] * 255f) << 24) | (b3 << 16) | (b2 << 8) | b);
					if (!flag3)
					{
						col = num8;
					}
				}
				if (item.NodeType == 3)
				{
					IntPtr orCreateTexture = TextureManager.GetOrCreateTexture(item.ImagePath);
					if (orCreateTexture != IntPtr.Zero)
					{
						uint imageColor = ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 1f, 1f, item.Opacity));
						if (previewCornerRadius > 0.01f)
						{
							windowDrawList.AddImageRounded(orCreateTexture, p_min, p_max, new Vector2(0f, 1f), new Vector2(1f, 0f), imageColor, previewCornerRadius, ImDrawFlags.RoundCornersAll);
						}
						else
						{
							windowDrawList.AddImage(orCreateTexture, p_min, p_max, new Vector2(0f, 1f), new Vector2(1f, 0f), imageColor);
						}
					}
					else
					{
						windowDrawList.AddRectFilled(p_min, p_max, 1149798536u, previewCornerRadius);
						windowDrawList.AddText(ImGui.GetFont(), ImGui.GetFontSize() * _canvasZoom, new Vector2(num6 + 5f * _canvasZoom, num7 + 5f * _canvasZoom), uint.MaxValue, "Image");
					}
					if (flag3)
					{
						windowDrawList.AddRect(p_min, p_max, col2, previewCornerRadius, ImDrawFlags.None, 2f * _canvasZoom);
					}
				}
				else
				{
					windowDrawList.AddRectFilled(p_min, p_max, col, previewCornerRadius);
					float num9 = Math.Max(1f, ((item.BorderThickness >= 0f) ? item.BorderThickness : configBorderThickness) * _canvasZoom);
					windowDrawList.AddRect(p_min, p_max, col2, previewCornerRadius, ImDrawFlags.None, flag3 ? Math.Max(2f * _canvasZoom, num9 + 1f * _canvasZoom) : num9);
					string text = (string.IsNullOrEmpty(item.CustomText) ? KeyDisplayNames.GetKeySymbol(item.KeyBind) : item.CustomText);
					if (item.NodeType == 1)
					{
						text = "KPS";
					}
					if (item.NodeType == 2)
					{
						text = "Total";
					}
					Vector2 vector = ImGui.GetFont().CalcTextSizeA(ImGui.GetFontSize() * _canvasZoom, float.MaxValue, 0f, text);
					windowDrawList.AddText(ImGui.GetFont(), ImGui.GetFontSize() * _canvasZoom, new Vector2(num6 + (num4 - vector.X) * 0.5f, num7 + (num5 - vector.Y) * 0.5f), uint.MaxValue, text);
				}
				if (flag && IsNodeSelectableInCurrentMode(item, iO) && IsMouseInsideNode(iO.MousePos, p_min, p_max))
				{
					flag2 = true;
					_hitNodesBuffer.Insert(0, item);
				}
			}
			if (flag && !_isDraggingNodes && !_isDraggingMarquee && ImGui.IsMouseClicked(ImGuiMouseButton.Left) && _hitNodesBuffer.Count > 0)
			{
				bool isDoubleClick = ConsumeCanvasDoubleClick(iO.MousePos);
				KVNode targetNode = PickNodeForCanvasClick(_hitNodesBuffer, isDoubleClick, iO.KeyCtrl);
				bool shouldStartDrag = targetNode != null && !iO.KeyCtrl && !isDoubleClick;
				ApplyCanvasClickSelection(targetNode, isDoubleClick, iO.KeyCtrl);
				if (shouldStartDrag && _selectedNodes.Contains(targetNode))
				{
					BeginNodeDrag();
				}
			}
			if (_isDraggingNodes)
			{
				if (ImGui.IsMouseDragging(ImGuiMouseButton.Left))
				{
					_dragTotalDeltaX += iO.MouseDelta.X / (keyViewerScale * _canvasZoom);
					_dragTotalDeltaY += iO.MouseDelta.Y / (keyViewerScale * _canvasZoom);
					if (iO.MouseDelta.X != 0f || iO.MouseDelta.Y != 0f)
					{
						_dragMoved = true;
					}
					ProcessSnappingAndAlignLines(list, displaySize, keyViewerScale);
				}
				if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
				{
					_isDraggingNodes = false;
					_activeAlignLines.Clear();
					if (_dragMoved)
					{
						Main.RequestSave();
					}
					_dragMoved = false;
				}
			}
			if (flag && !_isDraggingNodes && !flag2 && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
			{
				if (!iO.KeyCtrl)
				{
					_selectedNodes.Clear();
				}
				_isDraggingMarquee = true;
				_marqueeStart = iO.MousePos;
			}
			if (_isDraggingMarquee)
			{
				if (ImGui.IsMouseDragging(ImGuiMouseButton.Left))
				{
					Vector2 mousePos = iO.MousePos;
					float x = Math.Min(_marqueeStart.X, mousePos.X);
					float y = Math.Min(_marqueeStart.Y, mousePos.Y);
					float x2 = Math.Max(_marqueeStart.X, mousePos.X);
					float y2 = Math.Max(_marqueeStart.Y, mousePos.Y);
					windowDrawList.AddRectFilled(new Vector2(x, y), new Vector2(x2, y2), 1157605939u);
					windowDrawList.AddRect(new Vector2(x, y), new Vector2(x2, y2), 4294945331u);
				}
				if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
				{
					_isDraggingMarquee = false;
					Vector2 mousePos2 = iO.MousePos;
					float num12 = Math.Min(_marqueeStart.X, mousePos2.X);
					float num13 = Math.Min(_marqueeStart.Y, mousePos2.Y);
					float num14 = Math.Max(_marqueeStart.X, mousePos2.X);
					float num15 = Math.Max(_marqueeStart.Y, mousePos2.Y);
					foreach (KVNode item2 in list)
					{
						float num16 = keyViewerScale * item2.Scale * _canvasZoom;
						float num17 = item2.Width * num16;
						float num18 = item2.Height * num16;
						float num19 = canvasCenter.X + item2.PositionX * keyViewerScale * _canvasZoom;
						float num20 = canvasCenter.Y + item2.PositionY * keyViewerScale * _canvasZoom;
						bool flag5_m = false;
						if (_editMode == 2) {
							flag5_m = (item2.NodeType == 3);
						} else {
							flag5_m = (item2.NodeType != 3) && (!item2.IsUnselectable || iO.KeyShift);
						}
						if (num19 + num17 > num12 && num19 < num14 && num20 + num18 > num13 && num20 < num15 && flag5_m && !_selectedNodes.Contains(item2))
						{
							_selectedNodes.Add(item2);
						}
					}
				}
			}
			// 绘制智能局部对齐线 (AI 风格)
			foreach (var line in _activeAlignLines)
			{
				if (line.IsVertical)
				{
					float screenX = canvasCenter.X + line.Coord * keyViewerScale * _canvasZoom;
					float screenY1 = canvasCenter.Y + line.MinLimit * keyViewerScale * _canvasZoom;
					float screenY2 = canvasCenter.Y + line.MaxLimit * keyViewerScale * _canvasZoom;
					screenY1 = Math.Max(cursorScreenPos.Y, screenY1);
					screenY2 = Math.Min(cursorScreenPos.Y + contentRegionAvail.Y, screenY2);
					if (screenY1 < screenY2)
					{
						windowDrawList.AddLine(new Vector2(screenX, screenY1), new Vector2(screenX, screenY2), 4294967040u, 1.5f);
					}
				}
				else
				{
					float screenY = canvasCenter.Y + line.Coord * keyViewerScale * _canvasZoom;
					float screenX1 = canvasCenter.X + line.MinLimit * keyViewerScale * _canvasZoom;
					float screenX2 = canvasCenter.X + line.MaxLimit * keyViewerScale * _canvasZoom;
					screenX1 = Math.Max(cursorScreenPos.X, screenX1);
					screenX2 = Math.Min(cursorScreenPos.X + contentRegionAvail.X, screenX2);
					if (screenX1 < screenX2)
					{
						windowDrawList.AddLine(new Vector2(screenX1, screenY), new Vector2(screenX2, screenY), 4294967040u, 1.5f);
					}
				}
			}
			ImGui.EndChild();
			ImGui.SameLine();
			ImGui.BeginChild("FM_Props", new Vector2(0f, 0f), ImGuiChildFlags.Borders);
			ImGui.Text("属性面板");
			ImGui.Separator();
			if (_selectedNodes.Count == 0)
			{
				ImGui.Text("未选择节点");
			}
			else
			{
				bool flag6 = _selectedNodes.Count == 1;
				ImGui.Text($"已选择: {_selectedNodes.Count} 个节点");
				ImGui.Spacing();
				int selectedDepth = RenderDepth.ClampDepth(_selectedNodes[0].Depth);
				if (ImGui.SliderInt("\u6DF1\u5EA6##fm_depth", ref selectedDepth, RenderDepth.MinDepth, RenderDepth.MaxDepth))
				{
					foreach (KVNode selectedNode in _selectedNodes)
					{
						selectedNode.Depth = RenderDepth.ClampDepth(selectedDepth);
					}
					Main.RequestSave();
				}
				if (_editMode == 0)
				{
					if (flag6)
					{
						KVNode kVNode = _selectedNodes[0];
						int current_item = kVNode.NodeType;
						string[] items = new string[4] { "普通按键", "KPS面板", "Total面板", "背景贴图" };
						if (ImGui.Combo("节点类型", ref current_item, items, 4))
						{
							kVNode.NodeType = current_item;
							InputInterceptor.UpdateAllowedKeys();
							Main.RequestSave();
						}
						if (current_item == 0)
						{
							DrawKeyBindCapture(kVNode, "绑定按键", "fm_key_mode_bind");
						}
					}
					float v3 = _selectedNodes[0].PositionX;
					if (ImGui.DragFloat("X 位置", ref v3, 1f))
					{
						float num21 = v3 - _selectedNodes[0].PositionX;
						foreach (KVNode selectedNode4 in _selectedNodes)
						{
							selectedNode4.PositionX += num21;
						}
						Main.RequestSave();
					}
					float v4 = _selectedNodes[0].PositionY;
					if (ImGui.DragFloat("Y 位置", ref v4, 1f))
					{
						float num22 = v4 - _selectedNodes[0].PositionY;
						foreach (KVNode selectedNode5 in _selectedNodes)
						{
							selectedNode5.PositionY += num22;
						}
						Main.RequestSave();
					}
					float v5 = _selectedNodes[0].Width;
					if (ImGui.DragFloat("宽度", ref v5, 1f, 10f, 500f))
					{
						foreach (KVNode selectedNode6 in _selectedNodes)
						{
							selectedNode6.Width = v5;
						}
						Main.RequestSave();
					}
					float v6 = _selectedNodes[0].Height;
					if (ImGui.DragFloat("高度", ref v6, 1f, 10f, 500f))
					{
						foreach (KVNode selectedNode7 in _selectedNodes)
						{
							selectedNode7.Height = v6;
						}
						Main.RequestSave();
					}
					float v7 = _selectedNodes[0].BorderThickness;
					if (ImGui.DragFloat("边框粗细", ref v7, 0.1f, -1f, 10f, "%.1f (负数使用全局)"))
					{
						foreach (KVNode selectedNode8 in _selectedNodes)
						{
							selectedNode8.BorderThickness = v7;
						}
						Main.RequestSave();
					}
					float keyCornerRadius = _selectedNodes[0].CornerRadius;
					if (ImGui.DragFloat(KeyCornerRadiusLabel, ref keyCornerRadius, 0.5f, -1f, 256f))
					{
						keyCornerRadius = ClampNodeCornerRadius(keyCornerRadius);
						foreach (KVNode selectedNode in _selectedNodes)
						{
							selectedNode.CornerRadius = keyCornerRadius;
						}
						Main.RequestSave();
					}
					float v8 = _selectedNodes[0].Scale;
					if (ImGui.DragFloat("整体缩放", ref v8, 0.05f, 0.1f, 5f))
					{
						foreach (KVNode selectedNode9 in _selectedNodes)
						{
							selectedNode9.Scale = v8;
						}
						Main.RequestSave();
					}
					ImGui.Spacing();
					ImGui.Separator();
					ImGui.Text("颜色设置");
					bool v9 = _selectedNodes[0].UseCustomColor;
					if (ImGui.Checkbox("独立颜色", ref v9))
					{
						foreach (KVNode selectedNode10 in _selectedNodes)
						{
							selectedNode10.UseCustomColor = v9;
						}
						Main.RequestSave();
					}
					if (v9)
					{
						ImGui.Text("背景颜色");
						if (DrawColorPicker("常规##bg_norm", _selectedNodes[0].ColorBgNormal))
						{
							foreach (KVNode selectedNode11 in _selectedNodes)
							{
								Array.Copy(_selectedNodes[0].ColorBgNormal, selectedNode11.ColorBgNormal, 4);
							}
							Main.RequestSave();
						}
						ImGui.SameLine();
						if (DrawColorPicker("触发##bg_press", _selectedNodes[0].ColorBgPressed))
						{
							foreach (KVNode selectedNode12 in _selectedNodes)
							{
								Array.Copy(_selectedNodes[0].ColorBgPressed, selectedNode12.ColorBgPressed, 4);
							}
							Main.RequestSave();
						}
						ImGui.Text("边框颜色");
						if (DrawColorPicker("常规##bd_norm", _selectedNodes[0].ColorBorderNormal))
						{
							foreach (KVNode selectedNode13 in _selectedNodes)
							{
								Array.Copy(_selectedNodes[0].ColorBorderNormal, selectedNode13.ColorBorderNormal, 4);
							}
							Main.RequestSave();
						}
						ImGui.SameLine();
						if (DrawColorPicker("触发##bd_press", _selectedNodes[0].ColorBorderPressed))
						{
							foreach (KVNode selectedNode14 in _selectedNodes)
							{
								Array.Copy(_selectedNodes[0].ColorBorderPressed, selectedNode14.ColorBorderPressed, 4);
							}
							Main.RequestSave();
						}
						ImGui.Text("文本颜色");
						if (DrawColorPicker("常规##tx_norm", _selectedNodes[0].ColorTextNormal))
						{
							foreach (KVNode selectedNode15 in _selectedNodes)
							{
								Array.Copy(_selectedNodes[0].ColorTextNormal, selectedNode15.ColorTextNormal, 4);
							}
							Main.RequestSave();
						}
						ImGui.SameLine();
						if (DrawColorPicker("触发##tx_press", _selectedNodes[0].ColorTextPressed))
						{
							foreach (KVNode selectedNode16 in _selectedNodes)
							{
								Array.Copy(_selectedNodes[0].ColorTextPressed, selectedNode16.ColorTextPressed, 4);
							}
							Main.RequestSave();
						}
					}
					
					ImGui.Spacing();
					ImGui.Separator();
					ImGui.Text("键雨设置");
					
					int currentRainRow = _selectedNodes[0].RainRow;
					string[] rainRowItems = new string[] { "未设置", "顶层按键 (Row 1)", "底层按键 (Row 2)" };
					if (ImGui.Combo("键雨层级##rain_row", ref currentRainRow, rainRowItems, 3))
					{
						foreach (KVNode selectedNode in _selectedNodes)
						{
							selectedNode.RainRow = currentRainRow;
						}
						Main.RequestSave();
					}

					bool useCustomRain = _selectedNodes[0].UseCustomRain;
					if (ImGui.Checkbox("独立键雨设置##use_custom_rain", ref useCustomRain))
					{
						foreach (KVNode selectedNode in _selectedNodes)
						{
							selectedNode.UseCustomRain = useCustomRain;
						}
						Main.RequestSave();
					}

					if (useCustomRain)
					{
						float rainWidthRatio = _selectedNodes[0].RainWidthRatio;
						if (ImGui.SliderFloat("键雨宽度比例##rain_width", ref rainWidthRatio, 0.05f, 2.0f))
						{
							foreach (KVNode selectedNode in _selectedNodes)
							{
								selectedNode.RainWidthRatio = rainWidthRatio;
							}
							Main.RequestSave();
						}

						float rainYOffset = _selectedNodes[0].RainYOffset;
						if (ImGui.DragFloat("键雨Y轴偏移##rain_y_offset", ref rainYOffset, 1f))
						{
							foreach (KVNode selectedNode in _selectedNodes)
							{
								selectedNode.RainYOffset = rainYOffset;
							}
							Main.RequestSave();
						}

						if (DrawColorPicker("键雨颜色##rain_color", _selectedNodes[0].RainColor))
						{
							foreach (KVNode selectedNode in _selectedNodes)
							{
								if (selectedNode.RainColor == null || selectedNode.RainColor.Length != 4)
								{
									selectedNode.RainColor = new float[4];
								}
								Array.Copy(_selectedNodes[0].RainColor, selectedNode.RainColor, 4);
							}
							Main.RequestSave();
						}
					}
				}
				else if (_editMode == 1)
				{
					if (flag6)
					{
						KVNode kVNode2 = _selectedNodes[0];
						DrawKeyBindCapture(kVNode2, "绑定按键", "fm_text_mode_bind");
						string input4 = kVNode2.CustomText;
						if (ImGui.InputText("自定义文本", ref input4, 64u))
						{
							kVNode2.CustomText = input4;
							Main.RequestSave();
						}
						string input5 = kVNode2.KeyFontPath;
						if (ImGui.InputText("按键字体路径", ref input5, 256u))
						{
							kVNode2.KeyFontPath = input5;
							Main.RequestSave();
						}
						if (ImGui.IsItemDeactivatedAfterEdit())
						{
							ImportResourcePath(ref kVNode2.KeyFontPath, "Fonts", true);
						}
						if (kVNode2.NodeType == 0)
						{
							string input6 = kVNode2.CountFontPath;
							if (ImGui.InputText("计数字体路径", ref input6, 256u))
							{
								kVNode2.CountFontPath = input6;
								Main.RequestSave();
							}
							if (ImGui.IsItemDeactivatedAfterEdit())
							{
								ImportResourcePath(ref kVNode2.CountFontPath, "Fonts", true);
							}
						}
					}
					else
					{
						ImGui.Text("批量文本编辑不支持修改文本内容和字体路径");
					}
					bool hideCountText = _selectedNodes[0].HideCountText;
					if (ImGui.Checkbox("隐藏计数数字##fm_hide_count_text", ref hideCountText))
					{
						foreach (KVNode selectedNode in _selectedNodes)
						{
							selectedNode.HideCountText = hideCountText;
						}
						Main.RequestSave();
					}
					float v10 = _selectedNodes[0].TextScale;
					if (ImGui.DragFloat("文本缩放", ref v10, 0.05f, 0.1f, 5f))
					{
						foreach (KVNode selectedNode17 in _selectedNodes)
						{
							selectedNode17.TextScale = v10;
						}
						Main.RequestSave();
					}
					float v11 = _selectedNodes[0].TextOffsetX;
					if (ImGui.DragFloat("按键文字X偏移", ref v11, 1f))
					{
						foreach (KVNode selectedNode18 in _selectedNodes)
						{
							selectedNode18.TextOffsetX = v11;
						}
						Main.RequestSave();
					}
					float v12 = _selectedNodes[0].TextOffsetY;
					if (ImGui.DragFloat("按键文字Y偏移", ref v12, 1f))
					{
						foreach (KVNode selectedNode19 in _selectedNodes)
						{
							selectedNode19.TextOffsetY = v12;
						}
						Main.RequestSave();
					}
					float v13 = _selectedNodes[0].CountScale;
					if (ImGui.DragFloat("计数缩放", ref v13, 0.05f, 0.1f, 5f))
					{
						foreach (KVNode selectedNode20 in _selectedNodes)
						{
							selectedNode20.CountScale = v13;
						}
						Main.RequestSave();
					}
					float v14 = _selectedNodes[0].CountOffsetX;
					if (ImGui.DragFloat("计数文字X偏移", ref v14, 1f))
					{
						foreach (KVNode selectedNode21 in _selectedNodes)
						{
							selectedNode21.CountOffsetX = v14;
						}
						Main.RequestSave();
					}
					float v15 = _selectedNodes[0].CountOffsetY;
					if (ImGui.DragFloat("计数文字Y偏移", ref v15, 1f))
					{
						foreach (KVNode selectedNode22 in _selectedNodes)
						{
							selectedNode22.CountOffsetY = v15;
						}
						Main.RequestSave();
					}
					ImGui.Spacing();
					ImGui.Separator();
					ImGui.Text("文字描边");
					bool useCustomOutline = _selectedNodes[0].UseCustomOutline;
					if (ImGui.Checkbox("使用独立描边设置##fm_use_custom_outline", ref useCustomOutline))
					{
						foreach (KVNode selectedNode in _selectedNodes)
						{
							selectedNode.UseCustomOutline = useCustomOutline;
						}
						Main.RequestSave();
					}
					if (useCustomOutline)
					{
						bool keyOutlineEnabled = _selectedNodes[0].KeyTextOutlineEnabled;
						if (ImGui.Checkbox("开启按键文字描边##fm_key_text_outline", ref keyOutlineEnabled))
						{
							foreach (KVNode selectedNode in _selectedNodes)
							{
								selectedNode.KeyTextOutlineEnabled = keyOutlineEnabled;
							}
							Main.RequestSave();
						}
						if (keyOutlineEnabled)
						{
							ImGui.Indent();
							if (DrawColorPicker("按键文字描边颜色##fm_key_text_outline_color", _selectedNodes[0].KeyTextOutlineColor))
							{
								foreach (KVNode selectedNode in _selectedNodes)
								{
									CopyColor(_selectedNodes[0].KeyTextOutlineColor, ref selectedNode.KeyTextOutlineColor);
								}
								Main.RequestSave();
							}
							float keyOutlineThickness = _selectedNodes[0].KeyTextOutlineThickness;
							if (ImGui.DragFloat("按键文字描边粗细##fm_key_text_outline_thickness", ref keyOutlineThickness, 0.1f, 0f, 8f, "%.1f"))
							{
								foreach (KVNode selectedNode in _selectedNodes)
								{
									selectedNode.KeyTextOutlineThickness = keyOutlineThickness;
								}
								Main.RequestSave();
							}
							ImGui.Unindent();
						}

						bool countOutlineEnabled = _selectedNodes[0].CountTextOutlineEnabled;
						if (ImGui.Checkbox("开启计数文字描边##fm_count_text_outline", ref countOutlineEnabled))
						{
							foreach (KVNode selectedNode in _selectedNodes)
							{
								selectedNode.CountTextOutlineEnabled = countOutlineEnabled;
							}
							Main.RequestSave();
						}
						if (countOutlineEnabled)
						{
							ImGui.Indent();
							if (DrawColorPicker("计数文字描边颜色##fm_count_text_outline_color", _selectedNodes[0].CountTextOutlineColor))
							{
								foreach (KVNode selectedNode in _selectedNodes)
								{
									CopyColor(_selectedNodes[0].CountTextOutlineColor, ref selectedNode.CountTextOutlineColor);
								}
								Main.RequestSave();
							}
							float countOutlineThickness = _selectedNodes[0].CountTextOutlineThickness;
							if (ImGui.DragFloat("计数文字描边粗细##fm_count_text_outline_thickness", ref countOutlineThickness, 0.1f, 0f, 8f, "%.1f"))
							{
								foreach (KVNode selectedNode in _selectedNodes)
								{
									selectedNode.CountTextOutlineThickness = countOutlineThickness;
								}
								Main.RequestSave();
							}
							ImGui.Unindent();
						}
					}
					else
					{
						ImGui.Text("当前使用此配置的描边设置");
					}
				}
				else if (_editMode == 2)
				{
					if (flag6)
					{
						KVNode kVNode = _selectedNodes[0];
						int current_item = kVNode.NodeType;
						string[] items = new string[4] { "普通按键", "KPS面板", "Total面板", "背景贴图" };
						if (ImGui.Combo("节点类型", ref current_item, items, 4))
						{
							kVNode.NodeType = current_item;
							InputInterceptor.UpdateAllowedKeys();
							Main.RequestSave();
						}
						if (current_item == 3)
						{
							string input = kVNode.ImagePath ?? "";
							if (ImGui.InputText("图片绝对路径", ref input, 512u))
							{
								kVNode.ImagePath = input;
								Main.RequestSave();
							}
							if (ImGui.IsItemDeactivatedAfterEdit())
							{
								ImportResourcePath(ref kVNode.ImagePath, "Images", false);
							}
							bool v = kVNode.IsUnselectable;
							if (ImGui.Checkbox("不可选中 (在按键模式下锁定)", ref v))
							{
								kVNode.IsUnselectable = v;
								Main.RequestSave();
							}
							float v2 = kVNode.Opacity;
							if (ImGui.SliderFloat("透明度", ref v2, 0f, 1f))
							{
								kVNode.Opacity = v2;
								Main.RequestSave();
							}
						}
					}
					else
					{
						ImGui.Text("批量图片编辑暂不支持");
					}
					
					float v3 = _selectedNodes[0].PositionX;
					if (ImGui.DragFloat("X 位置", ref v3, 1f))
					{
						float num21 = v3 - _selectedNodes[0].PositionX;
						foreach (KVNode selectedNode in _selectedNodes)
						{
							selectedNode.PositionX += num21;
						}
						Main.RequestSave();
					}
					float v4 = _selectedNodes[0].PositionY;
					if (ImGui.DragFloat("Y 位置", ref v4, 1f))
					{
						float num22 = v4 - _selectedNodes[0].PositionY;
						foreach (KVNode selectedNode in _selectedNodes)
						{
							selectedNode.PositionY += num22;
						}
						Main.RequestSave();
					}
					float v5 = _selectedNodes[0].Width;
					if (ImGui.DragFloat("宽度", ref v5, 1f, 10f, 500f))
					{
						foreach (KVNode selectedNode in _selectedNodes)
						{
							selectedNode.Width = v5;
						}
						Main.RequestSave();
					}
					float v6 = _selectedNodes[0].Height;
					if (ImGui.DragFloat("高度", ref v6, 1f, 10f, 500f))
					{
						foreach (KVNode selectedNode in _selectedNodes)
						{
							selectedNode.Height = v6;
						}
						Main.RequestSave();
					}
					float imageCornerRadius = _selectedNodes[0].CornerRadius;
					if (ImGui.DragFloat(ImageCornerRadiusLabel, ref imageCornerRadius, 0.5f, -1f, 256f))
					{
						imageCornerRadius = ClampNodeCornerRadius(imageCornerRadius);
						foreach (KVNode selectedNode in _selectedNodes)
						{
							selectedNode.CornerRadius = imageCornerRadius;
						}
						Main.RequestSave();
					}
					float v8 = _selectedNodes[0].Scale;
					if (ImGui.DragFloat("整体缩放", ref v8, 0.05f, 0.1f, 5f))
					{
						foreach (KVNode selectedNode in _selectedNodes)
						{
							selectedNode.Scale = v8;
						}
						Main.RequestSave();
					}
				}
			}
			ImGui.EndChild();
		}
		ImGui.End();
	}

	private struct SnapCandidate
	{
		public float Value;
		public KVNode Node;
	}

	private static void ProcessSnappingAndAlignLines(List<KVNode> allNodes, Vector2 displaySize, float keyViewerScale)
	{
		_activeAlignLines.Clear();
		if (!_isDraggingNodes || _selectedNodes.Count == 0) return;

		float snapLimit = 5f / (keyViewerScale * _canvasZoom);

		List<KVNode> refNodes = new List<KVNode>();
		foreach (var n in allNodes)
		{
			if (!_selectedNodes.Contains(n))
			{
				refNodes.Add(n);
			}
		}

		List<SnapCandidate> refXList = new List<SnapCandidate>();
		refXList.Add(new SnapCandidate { Value = -displaySize.X * 0.5f, Node = null });
		refXList.Add(new SnapCandidate { Value = 0f, Node = null });
		refXList.Add(new SnapCandidate { Value = displaySize.X * 0.5f, Node = null });
		foreach (var r in refNodes)
		{
			float rw = r.Width * r.Scale;
			refXList.Add(new SnapCandidate { Value = r.PositionX, Node = r });
			refXList.Add(new SnapCandidate { Value = r.PositionX + rw * 0.5f, Node = r });
			refXList.Add(new SnapCandidate { Value = r.PositionX + rw, Node = r });
		}

		List<SnapCandidate> refYList = new List<SnapCandidate>();
		refYList.Add(new SnapCandidate { Value = -displaySize.Y * 0.5f, Node = null });
		refYList.Add(new SnapCandidate { Value = 0f, Node = null });
		refYList.Add(new SnapCandidate { Value = displaySize.Y * 0.5f, Node = null });
		foreach (var r in refNodes)
		{
			float rh = r.Height * r.Scale;
			refYList.Add(new SnapCandidate { Value = r.PositionY, Node = r });
			refYList.Add(new SnapCandidate { Value = r.PositionY + rh * 0.5f, Node = r });
			refYList.Add(new SnapCandidate { Value = r.PositionY + rh, Node = r });
		}

		float bestCorrX = 0f;
		float bestCorrY = 0f;
		bool hasCorrX = false;
		bool hasCorrY = false;

		foreach (var node in _selectedNodes)
		{
			if (!_dragStartPositions.ContainsKey(node)) continue;
			float targetX = _dragStartPositions[node].X + _dragTotalDeltaX;
			float w = node.Width * node.Scale;

			float[] checkX = new float[] { targetX, targetX + w * 0.5f, targetX + w };
			foreach (float tx in checkX)
			{
				foreach (var rx in refXList)
				{
					if (Math.Abs(tx - rx.Value) <= snapLimit)
					{
						float corrX = rx.Value - tx;
						if (!hasCorrX || Math.Abs(corrX) < Math.Abs(bestCorrX))
						{
							bestCorrX = corrX;
							hasCorrX = true;
						}
					}
				}
			}
		}

		foreach (var node in _selectedNodes)
		{
			if (!_dragStartPositions.ContainsKey(node)) continue;
			float targetY = _dragStartPositions[node].Y + _dragTotalDeltaY;
			float h = node.Height * node.Scale;

			float[] checkY = new float[] { targetY, targetY + h * 0.5f, targetY + h };
			foreach (float ty in checkY)
			{
				foreach (var ry in refYList)
				{
					if (Math.Abs(ty - ry.Value) <= snapLimit)
					{
						float corrY = ry.Value - ty;
						if (!hasCorrY || Math.Abs(corrY) < Math.Abs(bestCorrY))
						{
							bestCorrY = corrY;
							hasCorrY = true;
						}
					}
				}
			}
		}

		float finalDeltaX = _dragTotalDeltaX + (hasCorrX ? bestCorrX : 0f);
		float finalDeltaY = _dragTotalDeltaY + (hasCorrY ? bestCorrY : 0f);

		foreach (var node in _selectedNodes)
		{
			if (!_dragStartPositions.ContainsKey(node)) continue;
			node.PositionX = _dragStartPositions[node].X + finalDeltaX;
			node.PositionY = _dragStartPositions[node].Y + finalDeltaY;
		}

		Dictionary<float, List<float>> activeXCoords = new Dictionary<float, List<float>>();
		Dictionary<float, List<float>> activeYCoords = new Dictionary<float, List<float>>();

		foreach (var node in _selectedNodes)
		{
			float w = node.Width * node.Scale;
			float h = node.Height * node.Scale;

			float[] curX = new float[] { node.PositionX, node.PositionX + w * 0.5f, node.PositionX + w };
			foreach (float cx in curX)
			{
				foreach (var rx in refXList)
				{
					if (Math.Abs(cx - rx.Value) < 0.01f)
					{
						if (!activeXCoords.ContainsKey(rx.Value))
						{
							activeXCoords[rx.Value] = new List<float>();
						}
						activeXCoords[rx.Value].Add(node.PositionY);
						activeXCoords[rx.Value].Add(node.PositionY + h);
						if (rx.Node != null)
						{
							activeXCoords[rx.Value].Add(rx.Node.PositionY);
							activeXCoords[rx.Value].Add(rx.Node.PositionY + rx.Node.Height * rx.Node.Scale);
						}
						else
						{
							activeXCoords[rx.Value].Add(node.PositionY - 10f);
							activeXCoords[rx.Value].Add(node.PositionY + h + 10f);
						}
					}
				}
			}

			float[] curY = new float[] { node.PositionY, node.PositionY + h * 0.5f, node.PositionY + h };
			foreach (float cy in curY)
			{
				foreach (var ry in refYList)
				{
					if (Math.Abs(cy - ry.Value) < 0.01f)
					{
						if (!activeYCoords.ContainsKey(ry.Value))
						{
							activeYCoords[ry.Value] = new List<float>();
						}
						activeYCoords[ry.Value].Add(node.PositionX);
						activeYCoords[ry.Value].Add(node.PositionX + w);
						if (ry.Node != null)
						{
							activeYCoords[ry.Value].Add(ry.Node.PositionX);
							activeYCoords[ry.Value].Add(ry.Node.PositionX + ry.Node.Width * ry.Node.Scale);
						}
						else
						{
							activeYCoords[ry.Value].Add(node.PositionX - 10f);
							activeYCoords[ry.Value].Add(node.PositionX + w + 10f);
						}
					}
				}
			}
		}

		foreach (var kvp in activeXCoords)
		{
			_activeAlignLines.Add(new AlignLine { IsVertical = true, Coord = kvp.Key, MinLimit = kvp.Value.Min(), MaxLimit = kvp.Value.Max() });
		}
		foreach (var kvp in activeYCoords)
		{
			_activeAlignLines.Add(new AlignLine { IsVertical = false, Coord = kvp.Key, MinLimit = kvp.Value.Min(), MaxLimit = kvp.Value.Max() });
		}
	}

	private static void AlignSelectedNodes(int type)
	{
		if (_selectedNodes == null || _selectedNodes.Count <= 1) return;

		switch (type)
		{
			case 0:
				{
					float minX = _selectedNodes.Min(n => n.PositionX);
					foreach (var node in _selectedNodes)
					{
						node.PositionX = minX;
					}
					break;
				}
			case 1:
				{
					float avgCenterX = _selectedNodes.Average(n => n.PositionX + (n.Width * n.Scale) * 0.5f);
					foreach (var node in _selectedNodes)
					{
						node.PositionX = avgCenterX - (node.Width * node.Scale) * 0.5f;
					}
					break;
				}
			case 2:
				{
					float maxX = _selectedNodes.Max(n => n.PositionX + n.Width * n.Scale);
					foreach (var node in _selectedNodes)
					{
						node.PositionX = maxX - node.Width * node.Scale;
					}
					break;
				}
			case 3:
				{
					float minY = _selectedNodes.Min(n => n.PositionY);
					foreach (var node in _selectedNodes)
					{
						node.PositionY = minY;
					}
					break;
				}
			case 4:
				{
					float avgCenterY = _selectedNodes.Average(n => n.PositionY + (n.Height * n.Scale) * 0.5f);
					foreach (var node in _selectedNodes)
					{
						node.PositionY = avgCenterY - (node.Height * node.Scale) * 0.5f;
					}
					break;
				}
			case 5:
				{
					float maxY = _selectedNodes.Max(n => n.PositionY + n.Height * n.Scale);
					foreach (var node in _selectedNodes)
					{
						node.PositionY = maxY - node.Height * node.Scale;
					}
					break;
				}
			case 6:
				{
					if (_selectedNodes.Count < 3) break;
					var sorted = _selectedNodes.OrderBy(n => n.PositionX + (n.Width * n.Scale) * 0.5f).ToList();
					float cFirst = sorted[0].PositionX + (sorted[0].Width * sorted[0].Scale) * 0.5f;
					float cLast = sorted[sorted.Count - 1].PositionX + (sorted[sorted.Count - 1].Width * sorted[sorted.Count - 1].Scale) * 0.5f;
					float step = (cLast - cFirst) / (sorted.Count - 1);
					for (int i = 1; i < sorted.Count - 1; i++)
					{
						float targetCenterX = cFirst + i * step;
						sorted[i].PositionX = targetCenterX - (sorted[i].Width * sorted[i].Scale) * 0.5f;
					}
					break;
				}
			case 7:
				{
					if (_selectedNodes.Count < 3) break;
					var sorted = _selectedNodes.OrderBy(n => n.PositionY + (n.Height * n.Scale) * 0.5f).ToList();
					float cFirst = sorted[0].PositionY + (sorted[0].Height * sorted[0].Scale) * 0.5f;
					float cLast = sorted[sorted.Count - 1].PositionY + (sorted[sorted.Count - 1].Height * sorted[sorted.Count - 1].Scale) * 0.5f;
					float step = (cLast - cFirst) / (sorted.Count - 1);
					for (int i = 1; i < sorted.Count - 1; i++)
					{
						float targetCenterY = cFirst + i * step;
						sorted[i].PositionY = targetCenterY - (sorted[i].Height * sorted[i].Scale) * 0.5f;
					}
					break;
				}
		}
		Main.RequestSave();
	}
}
