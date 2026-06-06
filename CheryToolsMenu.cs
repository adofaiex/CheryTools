using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;
using ImGuiNET;
using UnityEngine;
using UnityModManagerNet;
using Vector2 = System.Numerics.Vector2;
using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;
using Object = UnityEngine.Object;

namespace CheryTools;

public class CheryToolsMenu : MonoBehaviour
{


	public static bool IsMenuOpen = false;

	private List<RichTextParser.ParsedSegment> _editingSegList;

	private int _editingSegIndex = -1;

	private int _editingBlockIndex = -1;

	private System.Numerics.Vector4 _editingColor;

	public static bool ShowToolsWindow = true;

	public static bool ShowKeyviewerWindow = false;

	public static bool ShowOverlayerWindow = false;

	public static bool ShowSettingsWindow = false;

	private int _currentToolTab;

	private int _waitingForKeyIndex = -1;

	private bool _waitingForToggleMenuKey = false;

	

	private int _selectedKVSidebarTab = -1;

	private static int _selectedOvSidebarTab = 0;

	private static int _selectedOvSidebarImgTab = 0;

	private void Update()
	{
		if (Input.GetKeyDown(Main.Settings.ToggleMenuKey))
		{
			IsMenuOpen = !IsMenuOpen;
		}
	}

	private bool DrawColorPicker(string label, ref float[] colorData)
	{
		System.Numerics.Vector4 col = new System.Numerics.Vector4(colorData[0], colorData[1], colorData[2], colorData[3]);
		if (ImGui.ColorEdit4(label, ref col, ImGuiColorEditFlags.NoInputs))
		{
			colorData[0] = col.X;
			colorData[1] = col.Y;
			colorData[2] = col.Z;
			colorData[3] = col.W;
			return true;
		}
		return false;
	}

	public void RenderUI()
	{
		//IL_20f3: Unknown result type (might be due to invalid IL or missing references)
		//IL_20fe: Expected O, but got Unknown
		//IL_0470: Unknown result type (might be due to invalid IL or missing references)
		//IL_047b: Expected O, but got Unknown
		//IL_0729: Unknown result type (might be due to invalid IL or missing references)
		//IL_072e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0730: Unknown result type (might be due to invalid IL or missing references)
		//IL_073c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0743: Invalid comparison between Unknown and I4
		//IL_0748: Unknown result type (might be due to invalid IL or missing references)
		//IL_0a37: Unknown result type (might be due to invalid IL or missing references)
		//IL_0a42: Expected O, but got Unknown
		//IL_188a: Unknown result type (might be due to invalid IL or missing references)
		//IL_1895: Expected O, but got Unknown
		//IL_1246: Unknown result type (might be due to invalid IL or missing references)
		//IL_124b: Unknown result type (might be due to invalid IL or missing references)
		//IL_124d: Unknown result type (might be due to invalid IL or missing references)
		//IL_1262: Unknown result type (might be due to invalid IL or missing references)
		//IL_127b: Unknown result type (might be due to invalid IL or missing references)
		//IL_1286: Expected O, but got Unknown
		if (FreeMakeEditor.IsOpen)
		{
			FreeMakeEditor.Draw();
		}
		if (!IsMenuOpen)
		{
			return;
		}
		ImGui.PushStyleColor(ImGuiCol.MenuBarBg, new System.Numerics.Vector4(0.16078432f, 0.2901961f, 0.47843137f, 1f));
		try
		{
			if (ImGui.BeginMainMenuBar())
			{
				try
				{
					ImGui.Text("CheryTools");
					ImGui.Separator();
					if (ImGui.MenuItem("Tools"))
					{
						ShowToolsWindow = !ShowToolsWindow;
					}
					if (ImGui.MenuItem("KeyViewer"))
					{
						ShowKeyviewerWindow = !ShowKeyviewerWindow;
					}
					if (ImGui.MenuItem("Overlayer"))
					{
						ShowOverlayerWindow = !ShowOverlayerWindow;
					}
					if (ImGui.MenuItem("设置"))
					{
						ShowSettingsWindow = !ShowSettingsWindow;
					}
				}
				finally
				{
					ImGui.EndMainMenuBar();
				}
			}
		}
		finally
		{
			ImGui.PopStyleColor(1);
		}
		if (ShowToolsWindow)
		{
			ImGui.SetNextWindowSize(new Vector2(450f, 300f), ImGuiCond.FirstUseEver);
			if (ImGui.Begin("Tools", ref ShowToolsWindow))
			{
				ImGui.BeginChild("Sidebar", new Vector2(100f, 0f), ImGuiChildFlags.Borders);
				if (ImGui.Selectable("优化", _currentToolTab == 0))
				{
					_currentToolTab = 0;
				}
				if (ImGui.Selectable("视觉", _currentToolTab == 1))
				{
					_currentToolTab = 1;
				}
				ImGui.EndChild();
				ImGui.SameLine();
				ImGui.BeginChild("Content", new Vector2(0f, 0f), ImGuiChildFlags.Borders);
				if (_currentToolTab == 0)
				{
					ImGui.Text("优化设置");
					ImGui.Separator();
					bool v = Main.Settings.EnableLegacyPauseFix;
					if (ImGui.Checkbox("开启老版本 Pause 修复", ref v))
					{
						Main.Settings.EnableLegacyPauseFix = v;
						Main.RequestSave();
					}
					ImGui.SameLine();
					ImGui.PushStyleColor(ImGuiCol.Text, new System.Numerics.Vector4(0.5f, 0.5f, 0.5f, 1f));
					ImGui.Text("修复新版本ADOFAI中老谱面的发卡弯暂停错误问题");
					ImGui.PopStyleColor();
				}
				else if (_currentToolTab == 1)
				{
					ImGui.Text("视觉");
					ImGui.Separator();
					
					bool hideNative = Main.Settings.HideNativeLevelName;
					if (ImGui.Checkbox("隐藏原版关卡名称 UI", ref hideNative))
					{
						Main.Settings.HideNativeLevelName = hideNative;
						VisualTweaks.ApplyLevelNameUI();
						Main.RequestSave();
					}
					
					ImGui.Separator();
					bool v2 = Main.Settings.EnableCustomPlanetColors;
					if (ImGui.Checkbox("启用自定义星球颜色", ref v2))
					{
						Main.Settings.EnableCustomPlanetColors = v2;
						Main.RequestSave();
						if (v2)
						{
							VisualTweaks.ApplyCustomColors();
						}
						else
						{
							VisualTweaks.RestoreDefaultColors();
						}
					}
					ImGui.Text("星球颜色设置");
					ImGui.Separator();
					ImGui.Text("火之行星");
					ImGui.SameLine(100f);
					if (DrawColorPicker("##RedPlanetColor", ref Main.Settings.RedPlanetColor))
					{
						Main.RequestSave();
						VisualTweaks.ApplyCustomColors();
					}
					ImGui.SameLine();
					ImGui.Text("拖尾");
					ImGui.SameLine(200f);
					if (DrawColorPicker("##RedTailColor", ref Main.Settings.RedTailColor))
					{
						Main.RequestSave();
						VisualTweaks.ApplyCustomColors();
					}
					ImGui.Text("冰之行星");
					ImGui.SameLine(100f);
					if (DrawColorPicker("##BluePlanetColor", ref Main.Settings.BluePlanetColor))
					{
						Main.RequestSave();
						VisualTweaks.ApplyCustomColors();
					}
					ImGui.SameLine();
					ImGui.Text("拖尾");
					ImGui.SameLine(200f);
					if (DrawColorPicker("##BlueTailColor", ref Main.Settings.BlueTailColor))
					{
						Main.RequestSave();
						VisualTweaks.ApplyCustomColors();
					}
					ImGui.Text("？之行星");
					ImGui.SameLine(100f);
					if (DrawColorPicker("##GreenPlanetColor", ref Main.Settings.GreenPlanetColor))
					{
						Main.RequestSave();
						VisualTweaks.ApplyCustomColors();
					}
					ImGui.SameLine();
					ImGui.Text("拖尾");
					ImGui.SameLine(200f);
					if (DrawColorPicker("##GreenTailColor", ref Main.Settings.GreenTailColor))
					{
						Main.RequestSave();
						VisualTweaks.ApplyCustomColors();
					}
				}
				ImGui.EndChild();
			}
			ImGui.End();
		}
		if (ShowKeyviewerWindow)
		{
			ImGui.SetNextWindowSize(new Vector2(550f, 450f), ImGuiCond.FirstUseEver);
			if (ImGui.Begin("KeyViewer", ref ShowKeyviewerWindow))
			{
				bool v3 = Main.Settings.EnableKeyViewer;
				if (ImGui.Checkbox("开启按键显示悬浮窗", ref v3))
				{
					Main.Settings.EnableKeyViewer = v3;
					Main.RequestSave();
				}
				ImGui.SameLine();
				bool v4 = Main.Settings.KeyViewerOnlyShowPlaying;
				if (ImGui.Checkbox("仅游戏时显示##kvplay", ref v4))
				{
					Main.Settings.KeyViewerOnlyShowPlaying = v4;
					Main.RequestSave();
				}
				bool v5 = Main.Settings.LimitInput;
				if (ImGui.Checkbox("限制输入", ref v5))
				{
					Main.Settings.LimitInput = v5;
					Main.RequestSave();
					InputInterceptor.UpdateAllowedKeys();
				}
				ImGui.SameLine(ImGui.GetWindowWidth() - 150f);
				if (ImGui.Button("重置统计数据") && (Object)KeyViewerManager.Instance != (Object)null)
				{
					KeyViewerManager.Instance.ResetCounts();
				}
				ImGui.Separator();
				if (_selectedKVSidebarTab == -1)
				{
					_selectedKVSidebarTab = Main.Settings.KeyViewerLayoutTab;
				}
				ImGui.BeginChild("KVSidebar", new Vector2(100f, 0f), ImGuiChildFlags.Borders);
				if (ImGui.Selectable("16 Key", _selectedKVSidebarTab == 0))
				{
					_selectedKVSidebarTab = 0;
					Main.Settings.KeyViewerLayoutTab = 0;
					Main.RequestSave();
				}
				if (ImGui.Selectable("12 Key", _selectedKVSidebarTab == 1))
				{
					_selectedKVSidebarTab = 1;
					Main.Settings.KeyViewerLayoutTab = 1;
					Main.RequestSave();
				}
				if (ImGui.Selectable("10 Key", _selectedKVSidebarTab == 2))
				{
					_selectedKVSidebarTab = 2;
					Main.Settings.KeyViewerLayoutTab = 2;
					Main.RequestSave();
				}
				if (ImGui.Selectable("8 Key", _selectedKVSidebarTab == 3))
				{
					_selectedKVSidebarTab = 3;
					Main.Settings.KeyViewerLayoutTab = 3;
					Main.RequestSave();
				}
				ImGui.Separator();
				if (ImGui.Selectable("脚键 (Foot)", _selectedKVSidebarTab == 4))
				{
					_selectedKVSidebarTab = 4;
				}
				ImGui.EndChild();
				ImGui.SameLine();
				ImGui.BeginChild("KVContent", new Vector2(0f, 0f), ImGuiChildFlags.Borders);
				if (_selectedKVSidebarTab >= 0 && _selectedKVSidebarTab <= 3)
				{
					ImGui.Text("按键绑定设置 (点击修改)");
					ImGui.Separator();
					int num = ((_selectedKVSidebarTab == 0) ? 16 : ((_selectedKVSidebarTab == 1) ? 12 : ((_selectedKVSidebarTab == 2) ? 10 : 8)));
					for (int i = 0; i < num; i++)
					{
						string arg = Main.Settings.KeyBindings[i];
						if (_waitingForKeyIndex == i)
						{
							arg = "[...]";
						}
						if (ImGui.Button($"{arg}##key{i}", new Vector2(50f, 30f)))
						{
							_waitingForKeyIndex = i;
						}
						if ((i + 1) % 8 != 0 && i != num - 1)
						{
							ImGui.SameLine();
						}
					}
					if (_waitingForKeyIndex != -1)
					{
						foreach (KeyCode value in Enum.GetValues(typeof(KeyCode)))
						{
							if (!Input.GetKeyDown(value) || (int)value == 323)
							{
								continue;
							}
							string text = ((object)value).ToString();
							if (_waitingForKeyIndex != -1)
							{
								Main.Settings.KeyBindings[_waitingForKeyIndex] = text;
								if (Main.Settings.Layout16K != null && _waitingForKeyIndex < Main.Settings.Layout16K.Count)
								{
									Main.Settings.Layout16K[_waitingForKeyIndex].KeyBind = text;
								}
								if (Main.Settings.Layout12K != null && _waitingForKeyIndex < Main.Settings.Layout12K.Count)
								{
									Main.Settings.Layout12K[_waitingForKeyIndex].KeyBind = text;
								}
								if (Main.Settings.Layout10K != null && _waitingForKeyIndex < Main.Settings.Layout10K.Count)
								{
									Main.Settings.Layout10K[_waitingForKeyIndex].KeyBind = text;
								}
								if (Main.Settings.Layout8K != null && _waitingForKeyIndex < Main.Settings.Layout8K.Count)
								{
									Main.Settings.Layout8K[_waitingForKeyIndex].KeyBind = text;
								}
								_waitingForKeyIndex = -1;
							}
							Main.RequestSave();
							break;
						}
					}
					ImGui.Spacing();
					ImGui.Text("自定义布局");
					ImGui.Separator();
					if (ImGui.Button("打开 FreeMake 编辑器", new Vector2(200f, 30f)))
					{
						FreeMakeEditor.IsOpen = true;
					}
					ImGui.SameLine();
					if (ImGui.Button("恢复默认布局", new Vector2(150f, 30f)))
					{
						if (Main.Settings.KeyViewerLayoutTab == 0)
						{
							Main.Settings.Layout16K = Main.Settings.GenerateDefaultKVLayout(16);
						}
						if (Main.Settings.KeyViewerLayoutTab == 1)
						{
							Main.Settings.Layout12K = Main.Settings.GenerateDefaultKVLayout(12);
						}
						if (Main.Settings.KeyViewerLayoutTab == 2)
						{
							Main.Settings.Layout10K = Main.Settings.GenerateDefaultKVLayout(10);
						}
						if (Main.Settings.KeyViewerLayoutTab == 3)
						{
							Main.Settings.Layout8K = Main.Settings.GenerateDefaultKVLayout(8);
						}
						Main.RequestSave();
						if ((Object)KeyViewerManager.Instance != (Object)null)
						{
							KeyViewerManager.Instance.ResetCounts();
						}
					}
					ImGui.Spacing();
					ImGui.Spacing();
					ImGui.Text("外观设置");
					ImGui.Separator();
					string input = Main.Settings.KeyViewerFontPath ?? "";
					if (ImGui.InputText("自定义字体路径 (如 D:/font.ttf 或 .otf)", ref input, 256u))
					{
						Main.Settings.KeyViewerFontPath = input;
						Main.RequestSave();
					}
					float v6 = Main.Settings.GlobalTextOffsetX;
					if (ImGui.DragFloat("全局按键文字X偏移", ref v6, 1f))
					{
						Main.Settings.GlobalTextOffsetX = v6;
						Main.RequestSave();
					}
					float v7 = Main.Settings.GlobalTextOffsetY;
					if (ImGui.DragFloat("全局按键文字Y偏移", ref v7, 1f))
					{
						Main.Settings.GlobalTextOffsetY = v7;
						Main.RequestSave();
					}
					float v8 = Main.Settings.GlobalCountOffsetX;
					if (ImGui.DragFloat("全局计数文字X偏移", ref v8, 1f))
					{
						Main.Settings.GlobalCountOffsetX = v8;
						Main.RequestSave();
					}
					float v9 = Main.Settings.GlobalCountOffsetY;
					if (ImGui.DragFloat("全局计数文字Y偏移", ref v9, 1f))
					{
						Main.Settings.GlobalCountOffsetY = v9;
						Main.RequestSave();
					}
					if (ImGui.Button("重新加载字体"))
					{
						ImGuiController.NeedsFontAtlasRebuild = true;
					}
					float v10 = Main.Settings.KeyViewerScale;
					if (ImGui.SliderFloat("缩放大小", ref v10, 0.5f, 3f))
					{
						Main.Settings.KeyViewerScale = v10;
						Main.RequestSave();
					}
					float v11 = Main.Settings.KeyViewerBorderThickness;
					if (ImGui.SliderFloat("全局默认边框粗细", ref v11, 0f, 5f))
					{
						Main.Settings.KeyViewerBorderThickness = v11;
						Main.RequestSave();
					}
					ImGui.Spacing();
					ImGui.Text("批量修改当前布局按键大小");
					if ((0u | (ImGui.DragFloat("全局宽度设定##bulk_w", ref Main.Settings.KeyViewerDefaultWidth, 1f, 10f, 500f) ? 1u : 0u) | (ImGui.DragFloat("全局高度设定##bulk_h", ref Main.Settings.KeyViewerDefaultHeight, 1f, 10f, 500f) ? 1u : 0u)) != 0)
					{
						Main.RequestSave();
					}
					if (ImGui.Button("应用大小到当前布局的所有按键"))
					{
						List<KVNode> activeNodes = KeyViewerManager.Instance.GetActiveNodes();
						if (activeNodes != null)
						{
							foreach (KVNode item in activeNodes)
							{
								item.Width = Main.Settings.KeyViewerDefaultWidth;
								item.Height = Main.Settings.KeyViewerDefaultHeight;
							}
							Main.RequestSave();
						}
					}
					ImGui.Spacing();
					ImGui.Text("颜色设置");
					ImGui.Separator();
					ImGui.Text("背景");
					if (DrawColorPicker("未按下##bg_norm", ref Main.Settings.KeyViewerColorBgNormal))
					{
						Main.RequestSave();
					}
					ImGui.SameLine();
					if (DrawColorPicker("触发##bg_press", ref Main.Settings.KeyViewerColorBgPressed))
					{
						Main.RequestSave();
					}
					ImGui.Text("边框");
					if (DrawColorPicker("未按下##border_norm", ref Main.Settings.KeyViewerColorBorderNormal))
					{
						Main.RequestSave();
					}
					ImGui.SameLine();
					if (DrawColorPicker("触发##border_press", ref Main.Settings.KeyViewerColorBorderPressed))
					{
						Main.RequestSave();
					}
					ImGui.Text("文本");
					if (DrawColorPicker("未按下##txt_norm", ref Main.Settings.KeyViewerColorTextNormal))
					{
						Main.RequestSave();
					}
					ImGui.SameLine();
					if (DrawColorPicker("触发##txt_press", ref Main.Settings.KeyViewerColorTextPressed))
					{
						Main.RequestSave();
					}
					ImGui.Text("底部统计文本");
					if (DrawColorPicker("KPS 颜色", ref Main.Settings.KeyViewerColorKps))
					{
						Main.RequestSave();
					}
					ImGui.SameLine();
					if (DrawColorPicker("Total 颜色", ref Main.Settings.KeyViewerColorTotal))
					{
						Main.RequestSave();
					}
					ImGui.Spacing();
					ImGui.Text("键雨 (Key Rain) 设置");
					ImGui.Separator();
					bool v12 = Main.Settings.EnableKeyRain;
					if (ImGui.Checkbox("开启键雨##rain_enable", ref v12))
					{
						Main.Settings.EnableKeyRain = v12;
						Main.RequestSave();
					}
					if (v12)
					{
						float v13 = Main.Settings.KeyRainSpeed;
						if (ImGui.SliderFloat("飞行速度##rain_speed", ref v13, 100f, 2000f))
						{
							Main.Settings.KeyRainSpeed = v13;
							Main.RequestSave();
						}
						float v14 = Main.Settings.KeyRainMaxHeight;
						if (ImGui.SliderFloat("消失距离##rain_maxh", ref v14, 100f, 1500f))
						{
							Main.Settings.KeyRainMaxHeight = v14;
							Main.RequestSave();
						}
						float v15 = Main.Settings.KeyRainYOffsetRow1;
						if (ImGui.SliderFloat("第一排高度偏移##rain_yoffset1", ref v15, -200f, 200f))
						{
							Main.Settings.KeyRainYOffsetRow1 = v15;
							Main.RequestSave();
						}
						float v16 = Main.Settings.KeyRainYOffsetRow2;
						if (ImGui.SliderFloat("第二排高度偏移##rain_yoffset2", ref v16, -200f, 200f))
						{
							Main.Settings.KeyRainYOffsetRow2 = v16;
							Main.RequestSave();
						}
						int current_item = Main.Settings.KeyRainFadeMode;
						if (ImGui.Combo("消失模式##rain_mode", ref current_item, "高度裁剪 (Clip)\0羽化透明 (Fade)\0"))
						{
							Main.Settings.KeyRainFadeMode = current_item;
							Main.RequestSave();
						}
						float v17 = Main.Settings.KeyRainWidthRatio1;
						if (ImGui.SliderFloat("第一排宽度比例##rain_w1", ref v17, 0.1f, 1f))
						{
							Main.Settings.KeyRainWidthRatio1 = v17;
							Main.RequestSave();
						}
						float v18 = Main.Settings.KeyRainWidthRatio2;
						if (ImGui.SliderFloat("第二排宽度比例##rain_w2", ref v18, 0.1f, 1f))
						{
							Main.Settings.KeyRainWidthRatio2 = v18;
							Main.RequestSave();
						}
						ImGui.Text("雨滴颜色");
						if (DrawColorPicker("第一排颜色##rain_c1", ref Main.Settings.KeyRainColorRow1))
						{
							Main.RequestSave();
						}
						ImGui.SameLine();
						if (DrawColorPicker("第二排颜色##rain_c2", ref Main.Settings.KeyRainColorRow2))
						{
							Main.RequestSave();
						}
					}
				}
				ImGui.EndChild();
			}
			ImGui.End();
		}
		if (ShowOverlayerWindow)
		{
			ImGui.SetNextWindowSize(new Vector2(450f, 450f), ImGuiCond.FirstUseEver);
			if (ImGui.Begin("Overlayer", ref ShowOverlayerWindow))
			{
				bool v21 = Main.Settings.OverlayerSystemEnabled;
				if (ImGui.Checkbox("开启 Overlayer 系统", ref v21))
				{
					Main.Settings.OverlayerSystemEnabled = v21;
					Main.RequestSave();
				}
				ImGui.SameLine();
				bool v22 = Main.Settings.OverlayerOnlyShowPlaying;
				if (ImGui.Checkbox("仅游戏时显示##ovplay", ref v22))
				{
					Main.Settings.OverlayerOnlyShowPlaying = v22;
					Main.RequestSave();
				}
				ImGui.SameLine(ImGui.GetWindowWidth() - 120f);
				bool v23 = Main.Settings.OverlayerEditMode;
				if (ImGui.Checkbox("解锁拖动", ref v23))
				{
					Main.Settings.OverlayerEditMode = v23;
					Main.RequestSave();
				}
				ImGui.Separator();
				ImGui.Spacing();
				if (v21 && ImGui.BeginTabBar("OvTabBar"))
				{
					if (ImGui.BeginTabItem("文本 (Texts)"))
					{
						List<OverlayerText> overlayerTexts = Main.Settings.OverlayerTexts;
						if (_selectedOvSidebarTab >= overlayerTexts.Count)
						{
							_selectedOvSidebarTab = overlayerTexts.Count - 1;
						}
						if (_selectedOvSidebarTab < 0 && overlayerTexts.Count > 0)
						{
							_selectedOvSidebarTab = 0;
						}
						ImGui.BeginChild("OvSidebar", new Vector2(120f, 0f), ImGuiChildFlags.Borders);
						if (ImGui.Button("新建文本 (+)", new Vector2(-1f, 0f)))
						{
							OverlayerText overlayerText = new OverlayerText();
							overlayerText.Name = $"新文本 {overlayerTexts.Count + 1}";
							overlayerTexts.Add(overlayerText);
							_selectedOvSidebarTab = overlayerTexts.Count - 1;
							Main.RequestSave();
						}
						ImGui.Separator();
						for (int k = 0; k < overlayerTexts.Count; k++)
						{
							string arg3 = (string.IsNullOrEmpty(overlayerTexts[k].Name) ? $"未命名 {k + 1}" : overlayerTexts[k].Name);
							if (ImGui.Selectable($"{arg3}##ov_tab_{k}", _selectedOvSidebarTab == k))
							{
								_selectedOvSidebarTab = k;
							}
						}
						ImGui.EndChild();
						ImGui.SameLine();
						ImGui.BeginChild("OvContent", new Vector2(0f, 0f), ImGuiChildFlags.Borders);
						if (_selectedOvSidebarTab >= 0 && _selectedOvSidebarTab < overlayerTexts.Count)
						{
							int selectedOvSidebarTab = _selectedOvSidebarTab;
							OverlayerText overlayerText2 = overlayerTexts[selectedOvSidebarTab];
							ImGui.PushID($"ov_block_{selectedOvSidebarTab}");
							try
							{
								string input2 = overlayerText2.Name;
								ImGui.SetNextItemWidth(200f);
								if (ImGui.InputText("模块名称", ref input2, 64u))
								{
									overlayerText2.Name = input2;
									Main.RequestSave();
								}
								ImGui.SameLine(ImGui.GetWindowWidth() - 80f);
								ImGui.PushStyleColor(ImGuiCol.Button, new System.Numerics.Vector4(0.8f, 0.2f, 0.2f, 1f));
								bool deletePressed = ImGui.Button("删除", new Vector2(60f, 24f));
								ImGui.PopStyleColor();

								if (deletePressed)
								{
									overlayerTexts.RemoveAt(selectedOvSidebarTab);
									Main.RequestSave();
									if (_selectedOvSidebarTab >= overlayerTexts.Count)
									{
										_selectedOvSidebarTab = overlayerTexts.Count - 1;
									}
								}
								else
								{
								bool v24 = overlayerText2.IsEnabled;
								if (ImGui.Checkbox("启用此模块", ref v24))
								{
									overlayerText2.IsEnabled = v24;
									Main.RequestSave();
								}
								ImGui.Separator();
								ImGui.Spacing();
								if (overlayerText2.IsEnabled)
								{
									string input3 = overlayerText2.TextFormat;
									if (ImGui.InputTextMultiline("公式", ref input3, 1024u, new Vector2(0f, 60f)))
									{
										overlayerText2.TextFormat = input3;
										Main.RequestSave();
									}
									if (ImGui.IsItemDeactivatedAfterEdit())
									{
										ImGuiController.NeedsFontAtlasRebuild = true;
									}
									ImGui.TextColored(new System.Numerics.Vector4(0.5f, 0.5f, 0.5f, 1f), "可用变量: {fps}, {kps}, {tot}, {p}, {te}, {acc}, {progress}...");
									float v25 = overlayerText2.FontSize;
									ImGui.SetNextItemWidth(150f);
									if (ImGui.SliderFloat("字号", ref v25, 10f, 250f))
									{
										overlayerText2.FontSize = v25;
										Main.RequestSave();
									}
									if (ImGui.IsItemDeactivatedAfterEdit())
									{
										ImGuiController.NeedsFontAtlasRebuild = true;
									}
									ImGui.SameLine();
									if (DrawColorPicker("默认颜色", ref overlayerText2.TextColor))
									{
										Main.RequestSave();
									}
									
									ImGui.Spacing();
									ImGui.Separator();
									ImGui.Text("位置与自适应锚点");
									
									float posX = overlayerText2.PositionX;
									float posY = overlayerText2.PositionY;
									ImGui.SetNextItemWidth(120f);
									if (ImGui.DragFloat("X 位置##posX", ref posX, 1f, -2000f, 4000f))
									{
										overlayerText2.PositionX = posX;
										Main.RequestSave();
									}
									ImGui.SameLine();
									ImGui.SetNextItemWidth(120f);
									if (ImGui.DragFloat("Y 位置##posY", ref posY, 1f, -2000f, 4000f))
									{
										overlayerText2.PositionY = posY;
										Main.RequestSave();
									}

									float pX = overlayerText2.PivotX;
									float pY = overlayerText2.PivotY;
									ImGui.SetNextItemWidth(120f);
									if (ImGui.SliderFloat("锚点 X (Pivot)##pivotX", ref pX, 0f, 1f, "%.2f (0左 1右)"))
									{
										overlayerText2.PivotX = pX;
										Main.RequestSave();
									}
									ImGui.SameLine();
									ImGui.SetNextItemWidth(120f);
									if (ImGui.SliderFloat("锚点 Y (Pivot)##pivotY", ref pY, 0f, 1f, "%.2f (0顶 1底)"))
									{
										overlayerText2.PivotY = pY;
										Main.RequestSave();
									}

									ImGui.Text("快速对齐:");
									ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(2f, 2f));
									ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(4f, 4f));
									
									Vector2 btnSize = new Vector2(28f, 28f);
									Vector2 displaySize = ImGui.GetIO().DisplaySize;
									
									if (ImGui.Button("↖##tl", btnSize)) AlignOvModule(overlayerText2, 0, displaySize); ImGui.SameLine();
									if (ImGui.Button("↑##tc", btnSize)) AlignOvModule(overlayerText2, 1, displaySize); ImGui.SameLine();
									if (ImGui.Button("↗##tr", btnSize)) AlignOvModule(overlayerText2, 2, displaySize);
									
									if (ImGui.Button("←##ml", btnSize)) AlignOvModule(overlayerText2, 3, displaySize); ImGui.SameLine();
									if (ImGui.Button("┼##cc", btnSize)) AlignOvModule(overlayerText2, 4, displaySize); ImGui.SameLine();
									if (ImGui.Button("→##mr", btnSize)) AlignOvModule(overlayerText2, 5, displaySize);
									
									if (ImGui.Button("↙##bl", btnSize)) AlignOvModule(overlayerText2, 6, displaySize); ImGui.SameLine();
									if (ImGui.Button("↓##bc", btnSize)) AlignOvModule(overlayerText2, 7, displaySize); ImGui.SameLine();
									if (ImGui.Button("↘##br", btnSize)) AlignOvModule(overlayerText2, 8, displaySize);
									
									ImGui.PopStyleVar(2);
									
									ImGui.Spacing();
									ImGui.Separator();
									ImGui.Text("文本排版与样式");
									
									float vSpacing = overlayerText2.LetterSpacing;
									if (ImGui.DragFloat("字符间距", ref vSpacing, 0.1f, -10f, 50f))
									{
										overlayerText2.LetterSpacing = vSpacing;
										Main.RequestSave();
									}
									
									float vLineHeight = overlayerText2.LineHeightOffset;
									if (ImGui.DragFloat("行高偏移", ref vLineHeight, 0.5f, -50f, 100f))
									{
										overlayerText2.LineHeightOffset = vLineHeight;
										Main.RequestSave();
									}
									
									bool vShadow = overlayerText2.EnableShadow;
									if (ImGui.Checkbox("开启阴影", ref vShadow))
									{
										overlayerText2.EnableShadow = vShadow;
										Main.RequestSave();
									}
									if (vShadow)
									{
										ImGui.Indent();
										if (DrawColorPicker("阴影颜色", ref overlayerText2.ShadowColor))
										{
											Main.RequestSave();
										}
										System.Numerics.Vector2 vShadowOffset = new System.Numerics.Vector2(overlayerText2.ShadowOffset[0], overlayerText2.ShadowOffset[1]);
										if (ImGui.DragFloat2("阴影偏移", ref vShadowOffset, 0.1f))
										{
											overlayerText2.ShadowOffset[0] = vShadowOffset.X;
											overlayerText2.ShadowOffset[1] = vShadowOffset.Y;
											Main.RequestSave();
										}
										ImGui.Unindent();
									}

									ImGui.Spacing();
									ImGui.Separator();
									string input4 = overlayerText2.FontPath;
									ImGui.SetNextItemWidth(250f);
									if (ImGui.InputText("自定义字体路径 (.ttf / .otf)", ref input4, 256u))
									{
										overlayerText2.FontPath = input4;
										Main.RequestSave();
									}
									ImGui.SameLine();
									if (ImGui.Button("应用字体"))
									{
										ImGuiController.NeedsFontAtlasRebuild = true;
									}
									int current_item3 = overlayerText2.Alignment;
									ImGui.SetNextItemWidth(150f);
									if (ImGui.Combo("对齐方式", ref current_item3, "居左\0居中\0居右\0"))
									{
										overlayerText2.Alignment = current_item3;
										Main.RequestSave();
									}
									
									ImGui.Separator();
									ImGui.Text("高级动画 JSON 配置");
									if (overlayerText2.Animations == null) overlayerText2.Animations = new List<OverlayerAnimation>();
									if (overlayerText2.Animations.Count == 0) overlayerText2.Animations.Add(new OverlayerAnimation());
									var mainAnim = overlayerText2.Animations[0];
									
									if (ImGui.Checkbox("启用动画", ref mainAnim.IsEnabled))
									{
										Main.RequestSave();
									}
									ImGui.SameLine();
									
									int triggerIndex = (int)mainAnim.Trigger;
									if (ImGui.Combo("触发条件", ref triggerIndex, "当点击时\0当Combo增加时\0"))
									{
										mainAnim.Trigger = (AnimationTrigger)triggerIndex;
										Main.RequestSave();
									}
									
									if (ImGui.InputTextMultiline("##JsonEditor", ref mainAnim.JsonString, 8192, new System.Numerics.Vector2(-1, 150)))
									{
										Main.RequestSave();
									}
									
									if (ImGui.Button("应用/解析 JSON", new System.Numerics.Vector2(150, 30)))
									{
										mainAnim.ParseJson();
										Main.RequestSave();
									}
									
									ImGui.SameLine();
									if (ImGui.Button("▶ 播放动画", new System.Numerics.Vector2(150, 30)))
									{
										mainAnim.ParseJson();
										if (OverlayerManager.Instance != null)
										{
											var state = OverlayerManager.Instance.GetAnimState(mainAnim);
											state.IsPlaying = true;
											state.CurrentTime = 0f;
										}
									}
									
									ImGui.Spacing();
									ImGui.Spacing();
									ImGui.Text("效果预览 (点击任意文字即可局部上色):");
									ImGui.PushStyleColor(ImGuiCol.ChildBg, new System.Numerics.Vector4(0.1f, 0.1f, 0.1f, 0.5f));
									try
									{
										bool isChildVisible = ImGui.BeginChild($"preview_ov_{selectedOvSidebarTab}", new Vector2(0f, 60f), ImGuiChildFlags.Borders);
										try
										{
											if (isChildVisible)
											{
												List<RichTextParser.ParsedSegment> list = RichTextParser.Parse(overlayerText2.TextFormat, new System.Numerics.Vector4(overlayerText2.TextColor[0], overlayerText2.TextColor[1], overlayerText2.TextColor[2], overlayerText2.TextColor[3]));
												bool flag2 = true;
												for (int l = 0; l < list.Count; l++)
												{
													RichTextParser.ParsedSegment interactiveSegment = list[l];
													string[] array = interactiveSegment.RenderText.Split('\n');
													for (int m = 0; m < array.Length; m++)
													{
														if (m > 0)
														{
															flag2 = true;
														}
														if (!flag2)
														{
															ImGui.SameLine(0f, 0f);
														}
														int num3 = 0;
														if ((Object)KeyViewerManager.Instance != (Object)null)
														{
															num3 = KeyViewerManager.Instance.CurrentKPS;
														}
														int totalHits = Main.Settings.TotalHits;
														string fmt = array[m].Replace("{kps}", num3.ToString()).Replace("{tot}", totalHits.ToString()).Replace("{fps}", "144")
															.Replace("{bpm}", "120")
															.Replace("{te}", "1")
															.Replace("{ve}", "2")
															.Replace("{ep}", "3")
															.Replace("{p}", "45")
															.Replace("{lp}", "3")
															.Replace("{vl}", "2")
															.Replace("{tl}", "1")
															.Replace("{miss}", "0")
															.Replace("{acc}", "98.50")
															.Replace("{xacc}", "97.00")
															.Replace("{progress}", "50.0");
														if (interactiveSegment.HasSizeTag && interactiveSegment.SizeValue > 0)
														{
															ImGui.SetWindowFontScale(interactiveSegment.SizeValue / 48f * ((overlayerText2.FontSize > 0) ? overlayerText2.FontSize : 100f) / ImGui.GetFontSize());
														}
														else if (interactiveSegment.HasSizeTag && interactiveSegment.SizeValue < 0)
														{
															ImGui.SetWindowFontScale(-interactiveSegment.SizeValue);
														}
														ImGui.TextColored(interactiveSegment.Color, fmt);
														if (interactiveSegment.HasSizeTag) ImGui.SetWindowFontScale(1.0f);
														if (ImGui.IsItemHovered())
														{
															ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
														}
														if (ImGui.IsItemClicked())
														{
															_editingBlockIndex = selectedOvSidebarTab;
															_editingSegIndex = l;
															_editingSegList = list;
															_editingColor = interactiveSegment.Color;
															ImGui.OpenPopup("ColorEditPopup");
														}
														flag2 = false;
													}
												}
												if (ImGui.BeginPopup("ColorEditPopup"))
												{
													ImGui.Text("修改该片段文字颜色");
													if (ImGui.ColorPicker4("##picker", ref _editingColor) && _editingSegList != null && _editingSegIndex >= 0 && _editingSegIndex < _editingSegList.Count)
													{
														_editingSegList[_editingSegIndex].Color = _editingColor;
														_editingSegList[_editingSegIndex].HasColorTag = true;
														StringBuilder stringBuilder = new StringBuilder();
														foreach (RichTextParser.ParsedSegment editingSeg in _editingSegList)
														{
															if (editingSeg.HasSizeTag && editingSeg.SizeValue < 0) stringBuilder.Append("<size=" + (-editingSeg.SizeValue * 100f).ToString("0.##") + "%>");
															else if (editingSeg.HasSizeTag && editingSeg.SizeValue > 0) stringBuilder.Append("<size=" + editingSeg.SizeValue.ToString("0.##") + ">");
															
															if (editingSeg.HasColorTag)
															{
																stringBuilder.Append("<color=#" + ColorToHex(editingSeg.Color) + ">" + editingSeg.RenderText + "</color>");
															}
															else
															{
																stringBuilder.Append(editingSeg.RenderText);
															}
															
															if (editingSeg.HasSizeTag) stringBuilder.Append("</size>");
														}
														overlayerText2.TextFormat = stringBuilder.ToString();
														Main.RequestSave();
													}
													if (ImGui.Button("恢复默认颜色", new Vector2(-1f, 0f)) && _editingSegList != null && _editingSegIndex >= 0 && _editingSegIndex < _editingSegList.Count)
													{
														_editingSegList[_editingSegIndex].HasColorTag = false;
														StringBuilder stringBuilder2 = new StringBuilder();
														foreach (RichTextParser.ParsedSegment editingSeg2 in _editingSegList)
														{
															if (editingSeg2.HasSizeTag && editingSeg2.SizeValue < 0) stringBuilder2.Append("<size=" + (-editingSeg2.SizeValue * 100f).ToString("0.##") + "%>");
															else if (editingSeg2.HasSizeTag && editingSeg2.SizeValue > 0) stringBuilder2.Append("<size=" + editingSeg2.SizeValue.ToString("0.##") + ">");

															if (editingSeg2.HasColorTag)
															{
																stringBuilder2.Append("<color=#" + ColorToHex(editingSeg2.Color) + ">" + editingSeg2.RenderText + "</color>");
															}
															else
															{
																stringBuilder2.Append(editingSeg2.RenderText);
															}
															
															if (editingSeg2.HasSizeTag) stringBuilder2.Append("</size>");
														}
														overlayerText2.TextFormat = stringBuilder2.ToString();
														Main.RequestSave();
														ImGui.CloseCurrentPopup();
													}
													ImGui.EndPopup();
												}
											}
										}
										finally
										{
											ImGui.EndChild();
										}
									}
									finally
									{
										ImGui.PopStyleColor();
									}
								}
							}
							}
							finally
							{
								ImGui.PopID();
							}
						}
						else
						{
							ImGui.TextColored(new System.Numerics.Vector4(0.5f, 0.5f, 0.5f, 1f), "请在左侧新建或选择一个模块");
						}
						ImGui.EndChild();
						ImGui.EndTabItem();
					}
					if (ImGui.BeginTabItem("图片 (Images)"))
					{
						List<OverlayerImage> overlayerImages = Main.Settings.OverlayerImages;
						if (_selectedOvSidebarImgTab >= overlayerImages.Count)
						{
							_selectedOvSidebarImgTab = overlayerImages.Count - 1;
						}
						if (_selectedOvSidebarImgTab < 0 && overlayerImages.Count > 0)
						{
							_selectedOvSidebarImgTab = 0;
						}
						ImGui.BeginChild("OvImgSidebar", new Vector2(120f, 0f), ImGuiChildFlags.Borders);
						if (ImGui.Button("新建图片 (+)", new Vector2(-1f, 0f)))
						{
							overlayerImages.Add(new OverlayerImage());
							_selectedOvSidebarImgTab = overlayerImages.Count - 1;
							Main.RequestSave();
						}
						ImGui.Separator();
						for (int n = 0; n < overlayerImages.Count; n++)
						{
							string arg4 = $"图片 {n + 1}";
							if (ImGui.Selectable($"{arg4}##ov_imgtab_{n}", _selectedOvSidebarImgTab == n))
							{
								_selectedOvSidebarImgTab = n;
							}
						}
						ImGui.EndChild();
						ImGui.SameLine();
						ImGui.BeginChild("OvImgContent", new Vector2(0f, 0f), ImGuiChildFlags.Borders);
						if (_selectedOvSidebarImgTab >= 0 && _selectedOvSidebarImgTab < overlayerImages.Count)
						{
							int selectedOvSidebarImgTab = _selectedOvSidebarImgTab;
							OverlayerImage overlayerImage = overlayerImages[selectedOvSidebarImgTab];
							ImGui.PushID($"ov_imgblock_{selectedOvSidebarImgTab}");
							try
							{
								ImGui.Text($"图片配置 ({selectedOvSidebarImgTab + 1})");
								ImGui.SameLine(ImGui.GetWindowWidth() - 80f);
								ImGui.PushStyleColor(ImGuiCol.Button, new System.Numerics.Vector4(0.8f, 0.2f, 0.2f, 1f));
								bool deletePressed = ImGui.Button("删除", new Vector2(60f, 24f));
								ImGui.PopStyleColor();

								if (deletePressed)
								{
									overlayerImages.RemoveAt(selectedOvSidebarImgTab);
									Main.RequestSave();
									if (_selectedOvSidebarImgTab >= overlayerImages.Count)
									{
										_selectedOvSidebarImgTab = overlayerImages.Count - 1;
									}
								}
								else
								{
									bool v26 = overlayerImage.IsEnabled;
									if (ImGui.Checkbox("启用此图片", ref v26))
									{
										overlayerImage.IsEnabled = v26;
										Main.RequestSave();
									}
									ImGui.Separator();
									ImGui.Spacing();
									if (overlayerImage.IsEnabled)
									{
										string input5 = overlayerImage.ImagePath ?? "";
										if (ImGui.InputText("图片绝对路径", ref input5, 512u))
										{
											overlayerImage.ImagePath = input5;
											Main.RequestSave();
										}
										float v27 = overlayerImage.Scale;
										if (ImGui.SliderFloat("缩放比例", ref v27, 0.05f, 10f))
										{
											overlayerImage.Scale = v27;
											Main.RequestSave();
										}
										float v28 = overlayerImage.Rotation;
										if (ImGui.SliderFloat("旋转角度", ref v28, -360f, 360f))
										{
											overlayerImage.Rotation = v28;
											Main.RequestSave();
										}
										float v29 = overlayerImage.Opacity;
										if (ImGui.SliderFloat("不透明度", ref v29, 0f, 1f))
										{
											overlayerImage.Opacity = v29;
											Main.RequestSave();
										}

										ImGui.Spacing();
										ImGui.Separator();
										ImGui.Text("位置与自适应锚点");
										
										float imgPosX = overlayerImage.PositionX;
										float imgPosY = overlayerImage.PositionY;
										ImGui.SetNextItemWidth(120f);
										if (ImGui.DragFloat("X 位置##imgPosX", ref imgPosX, 1f, -2000f, 4000f))
										{
											overlayerImage.PositionX = imgPosX;
											Main.RequestSave();
										}
										ImGui.SameLine();
										ImGui.SetNextItemWidth(120f);
										if (ImGui.DragFloat("Y 位置##imgPosY", ref imgPosY, 1f, -2000f, 4000f))
										{
											overlayerImage.PositionY = imgPosY;
											Main.RequestSave();
										}

										float imgPX = overlayerImage.PivotX;
										float imgPY = overlayerImage.PivotY;
										ImGui.SetNextItemWidth(120f);
										if (ImGui.SliderFloat("锚点 X (Pivot)##imgPivotX", ref imgPX, 0f, 1f, "%.2f (0左 1右)"))
										{
											overlayerImage.PivotX = imgPX;
											Main.RequestSave();
										}
										ImGui.SameLine();
										ImGui.SetNextItemWidth(120f);
										if (ImGui.SliderFloat("锚点 Y (Pivot)##imgPivotY", ref imgPY, 0f, 1f, "%.2f (0顶 1底)"))
										{
											overlayerImage.PivotY = imgPY;
											Main.RequestSave();
										}

										ImGui.Text("快速对齐:");
										ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(2f, 2f));
										ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(4f, 4f));
										
										Vector2 imgBtnSize = new Vector2(28f, 28f);
										Vector2 displaySize = ImGui.GetIO().DisplaySize;
										
										if (ImGui.Button("↖##img_tl", imgBtnSize)) AlignOvImage(overlayerImage, 0, displaySize); ImGui.SameLine();
										if (ImGui.Button("↑##img_tc", imgBtnSize)) AlignOvImage(overlayerImage, 1, displaySize); ImGui.SameLine();
										if (ImGui.Button("↗##img_tr", imgBtnSize)) AlignOvImage(overlayerImage, 2, displaySize);
										
										if (ImGui.Button("←##img_ml", imgBtnSize)) AlignOvImage(overlayerImage, 3, displaySize); ImGui.SameLine();
										if (ImGui.Button("┼##img_cc", imgBtnSize)) AlignOvImage(overlayerImage, 4, displaySize); ImGui.SameLine();
										if (ImGui.Button("→##img_mr", imgBtnSize)) AlignOvImage(overlayerImage, 5, displaySize);
										
										if (ImGui.Button("↙##img_bl", imgBtnSize)) AlignOvImage(overlayerImage, 6, displaySize); ImGui.SameLine();
										if (ImGui.Button("↓##img_bc", imgBtnSize)) AlignOvImage(overlayerImage, 7, displaySize); ImGui.SameLine();
										if (ImGui.Button("↘##img_br", imgBtnSize)) AlignOvImage(overlayerImage, 8, displaySize);
										
										ImGui.PopStyleVar(2);
									}
								}
							}
							finally
							{
								ImGui.PopID();
							}
						}
						else
						{
							ImGui.TextColored(new System.Numerics.Vector4(0.5f, 0.5f, 0.5f, 1f), "请在左侧新建或选择一个模块");
						}
						ImGui.EndChild();
						ImGui.EndTabItem();
					}
					ImGui.EndTabBar();
				}
			}
			ImGui.End();
		}
		if (!ShowSettingsWindow)
		{
			return;
		}
		ImGui.SetNextWindowSize(new Vector2(300f, 150f), ImGuiCond.FirstUseEver);
		if (ImGui.Begin("设置", ref ShowSettingsWindow))
		{
			string hotkeyLabel = _waitingForToggleMenuKey ? "[等待按键输入...]" : Main.Settings.ToggleMenuKey.ToString();
			ImGui.AlignTextToFramePadding();
			ImGui.Text("呼出设置快捷键:");
			ImGui.SameLine();
			if (ImGui.Button($"{hotkeyLabel}##ToggleMenuKeyBtn", new Vector2(120f, 0f)))
			{
				_waitingForToggleMenuKey = true;
			}
			if (_waitingForToggleMenuKey)
			{
				foreach (KeyCode value in Enum.GetValues(typeof(KeyCode)))
				{
					if (!Input.GetKeyDown(value) || (int)value == 323)
					{
						continue;
					}
					Main.Settings.ToggleMenuKey = value;
					_waitingForToggleMenuKey = false;
					InputInterceptor.UpdateAllowedKeys();
					Main.RequestSave();
					break;
				}
			}
			ImGui.Separator();
			ImGui.Spacing();
			string text3 = Path.Combine(Application.dataPath, "../CheryTools_Settings_Backup.xml");
			if (ImGui.Button("导出配置 (XML)"))
			{
				try
				{
					Main.Settings.Save(Main.ModEntry);
					File.Copy(Path.Combine(Main.ModEntry.Path, "Settings.xml"), text3, overwrite: true);
					Main.Logger.Log("Settings exported to: " + text3);
				}
				catch (Exception ex)
				{
					Main.Logger.Log("Failed to export settings: " + ex.ToString());
				}
			}
			ImGui.SameLine();
			if (ImGui.Button("导入配置 (XML)"))
			{
				try
				{
					if (File.Exists(text3))
					{
						string destFileName = Path.Combine(Main.ModEntry.Path, "Settings.xml");
						File.Copy(text3, destFileName, overwrite: true);
						Main.Settings = UnityModManager.ModSettings.Load<Settings>(Main.ModEntry);
						Main.Settings.InitNulls();
						if ((Object)KeyViewerManager.Instance != (Object)null)
						{
							KeyViewerManager.Instance.RefreshKeys();
						}
						InputInterceptor.UpdateAllowedKeys();
						Main.Logger.Log("Settings imported successfully from: " + text3);
					}
				}
				catch (Exception ex2)
				{
					Main.Logger.Log("Failed to import settings: " + ex2.ToString());
				}
			}
			if (File.Exists(text3))
			{
				ImGui.SameLine();
				ImGui.TextColored(new System.Numerics.Vector4(0.5f, 0.5f, 0.5f, 1f), "配置已保存在游戏根目录!");
			}
			ImGui.Separator();
			ImGui.Spacing();
			if (ImGui.Button("关闭菜单"))
			{
				IsMenuOpen = false;
			}
		}
		ImGui.End();
	}

	private string ColorToHex(System.Numerics.Vector4 c)
	{
		int num = (int)(c.X * 255f);
		int num2 = (int)(c.Y * 255f);
		int num3 = (int)(c.Z * 255f);
		int num4 = (int)(c.W * 255f);
		return $"{num:X2}{num2:X2}{num3:X2}{num4:X2}";
	}

	private System.Numerics.Vector4 ParseHexColor(string hex, System.Numerics.Vector4 fallback)
	{
		if (hex.Length == 6 || hex.Length == 8)
		{
			try
			{
				float x = (float)Convert.ToInt32(hex.Substring(0, 2), 16) / 255f;
				float y = (float)Convert.ToInt32(hex.Substring(2, 2), 16) / 255f;
				float z = (float)Convert.ToInt32(hex.Substring(4, 2), 16) / 255f;
				float w = 1f;
				if (hex.Length == 8)
				{
					w = (float)Convert.ToInt32(hex.Substring(6, 2), 16) / 255f;
				}
				return new System.Numerics.Vector4(x, y, z, w);
			}
			catch
			{
				return fallback;
			}
		}
		return fallback;
	}

	private static void AlignOvModule(OverlayerText ovText, int type, Vector2 displaySize)
	{
		if (ovText == null) return;

		switch (type)
		{
			case 0:
				ovText.PositionX = 20f;
				ovText.PositionY = 20f;
				ovText.PivotX = 0f;
				ovText.PivotY = 0f;
				ovText.Alignment = 0;
				break;
			case 1:
				ovText.PositionX = displaySize.X * 0.5f;
				ovText.PositionY = 20f;
				ovText.PivotX = 0.5f;
				ovText.PivotY = 0f;
				ovText.Alignment = 1;
				break;
			case 2:
				ovText.PositionX = displaySize.X - 20f;
				ovText.PositionY = 20f;
				ovText.PivotX = 1f;
				ovText.PivotY = 0f;
				ovText.Alignment = 2;
				break;
			case 3:
				ovText.PositionX = 20f;
				ovText.PositionY = displaySize.Y * 0.5f;
				ovText.PivotX = 0f;
				ovText.PivotY = 0.5f;
				ovText.Alignment = 0;
				break;
			case 4:
				ovText.PositionX = displaySize.X * 0.5f;
				ovText.PositionY = displaySize.Y * 0.5f;
				ovText.PivotX = 0.5f;
				ovText.PivotY = 0.5f;
				ovText.Alignment = 1;
				break;
			case 5:
				ovText.PositionX = displaySize.X - 20f;
				ovText.PositionY = displaySize.Y * 0.5f;
				ovText.PivotX = 1f;
				ovText.PivotY = 0.5f;
				ovText.Alignment = 2;
				break;
			case 6:
				ovText.PositionX = 20f;
				ovText.PositionY = displaySize.Y - 20f;
				ovText.PivotX = 0f;
				ovText.PivotY = 1f;
				ovText.Alignment = 0;
				break;
			case 7:
				ovText.PositionX = displaySize.X * 0.5f;
				ovText.PositionY = displaySize.Y - 20f;
				ovText.PivotX = 0.5f;
				ovText.PivotY = 1f;
				ovText.Alignment = 1;
				break;
			case 8:
				ovText.PositionX = displaySize.X - 20f;
				ovText.PositionY = displaySize.Y - 20f;
				ovText.PivotX = 1f;
				ovText.PivotY = 1f;
				ovText.Alignment = 2;
				break;
		}
		Main.RequestSave();
	}

	private static void AlignOvImage(OverlayerImage ovImg, int type, Vector2 displaySize)
	{
		if (ovImg == null) return;

		switch (type)
		{
			case 0:
				ovImg.PositionX = 20f;
				ovImg.PositionY = 20f;
				ovImg.PivotX = 0f;
				ovImg.PivotY = 0f;
				break;
			case 1:
				ovImg.PositionX = displaySize.X * 0.5f;
				ovImg.PositionY = 20f;
				ovImg.PivotX = 0.5f;
				ovImg.PivotY = 0f;
				break;
			case 2:
				ovImg.PositionX = displaySize.X - 20f;
				ovImg.PositionY = 20f;
				ovImg.PivotX = 1f;
				ovImg.PivotY = 0f;
				break;
			case 3:
				ovImg.PositionX = 20f;
				ovImg.PositionY = displaySize.Y * 0.5f;
				ovImg.PivotX = 0f;
				ovImg.PivotY = 0.5f;
				break;
			case 4:
				ovImg.PositionX = displaySize.X * 0.5f;
				ovImg.PositionY = displaySize.Y * 0.5f;
				ovImg.PivotX = 0.5f;
				ovImg.PivotY = 0.5f;
				break;
			case 5:
				ovImg.PositionX = displaySize.X - 20f;
				ovImg.PositionY = displaySize.Y * 0.5f;
				ovImg.PivotX = 1f;
				ovImg.PivotY = 0.5f;
				break;
			case 6:
				ovImg.PositionX = 20f;
				ovImg.PositionY = displaySize.Y - 20f;
				ovImg.PivotX = 0f;
				ovImg.PivotY = 1f;
				break;
			case 7:
				ovImg.PositionX = displaySize.X * 0.5f;
				ovImg.PositionY = displaySize.Y - 20f;
				ovImg.PivotX = 0.5f;
				ovImg.PivotY = 1f;
				break;
			case 8:
				ovImg.PositionX = displaySize.X - 20f;
				ovImg.PositionY = displaySize.Y - 20f;
				ovImg.PivotX = 1f;
				ovImg.PivotY = 1f;
				break;
		}
		Main.RequestSave();
	}
}
