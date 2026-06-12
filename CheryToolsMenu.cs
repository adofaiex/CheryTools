using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
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
	private static int _lastTextFormatCursorPos = 0;

	private unsafe static int TextFormatCallback(ImGuiInputTextCallbackData* data)
	{
		_lastTextFormatCursorPos = data->CursorPos;
		return 0;
	}

	private void InsertTagToFormat(OverlayerText ovText, string tag, bool placeCursorInside)
	{
		if (ovText == null || tag == null)
		{
			return;
		}

		string currentText = ovText.TextFormat ?? "";
		int pos = Math.Max(0, Math.Min(_lastTextFormatCursorPos, currentText.Length));
		ovText.TextFormat = currentText.Insert(pos, tag);
		if (placeCursorInside)
		{
			int contentStart = tag.IndexOf('>') + 1;
			_lastTextFormatCursorPos = pos + (contentStart > 0 ? contentStart : tag.Length);
		}
		else
		{
			_lastTextFormatCursorPos = pos + tag.Length;
		}

		Main.RequestSave();
		ImGuiController.NeedsFontAtlasRebuild = true;
	}

	private void DrawOverlayerTagInsertPopup(OverlayerText ovText)
	{
		ImGui.SetNextWindowSize(new Vector2(640f, 430f), ImGuiCond.Appearing);
		if (!ImGui.BeginPopup("TagSelectorPopup"))
		{
			return;
		}

		ImGui.Text("\u8BF7\u9009\u62E9\u8981\u63D2\u5165\u7684 Tag:");
		ImGui.Separator();

		if (ImGui.BeginTabBar("TagTabs"))
		{
			if (ImGui.BeginTabItem("\u6E38\u620F\u6570\u636E"))
			{
				DrawTagTable("GameTagTable", ovText, new (string Tag, string Desc)[]
				{
					("{fps}", "\u5F53\u524D FPS"),
					("{fps:1}", "\u5F53\u524D FPS\uFF0C\u6700\u591A 1 \u4F4D\u5C0F\u6570"),
					("{kps}", "\u5F53\u524D\u6BCF\u79D2\u6309\u952E\u6570"),
					("{tot}", "\u603B\u6309\u952E\u6B21\u6570"),
					("{combo}", "\u5F53\u524D Pure Combo"),
					("{combo:p}", "\u5F53\u524D Perfect Combo"),
					("{music}", "\u5F53\u524D\u66F2\u76EE\u6807\u9898"),
					("{ttile}", "\u603B\u8F68\u9053\u6570\u91CF"),
					("{atile}", "\u7ECF\u8FC7\u7684\u8F68\u9053\u6570\u91CF"),
					("{level}", "关卡制作者"),
					("{x}", "关卡倍速")
				});
				ImGui.EndTabItem();
			}

			if (ImGui.BeginTabItem("BPM"))
			{
				DrawTagTable("BpmTagTable", ovText, new (string Tag, string Desc)[]
				{
					("{bpm}", "\u57FA\u7840 BPM"),
					("{tbpm}", "\u542B\u8F68\u9053\u901F\u5EA6\u4E58\u6570\u7684 BPM"),
					("{cbpm}", "\u57FA\u4E8E\u5730\u677F\u65F6\u95F4\u7684\u5F53\u524D\u771F\u5B9E BPM"),
					("{cur}", "\u5F53\u524D\u771F\u5B9E BPM \u4E0B\u7684\u6BCF\u79D2\u70B9\u51FB\u6B21\u6570")
				});
				ImGui.EndTabItem();
			}

			if (ImGui.BeginTabItem("\u65F6\u95F4\u65E5\u671F"))
			{
				DrawTagTable("TimeTagTable", ovText, new (string Tag, string Desc)[]
				{
					("{maptime}", "\u5730\u56FE\u603B\u65F6\u95F4"),
					("{maptime:p}", "\u5730\u56FE\u5DF2\u6E38\u73A9\u65F6\u95F4"),
					("{musictime}", "\u97F3\u4E50\u603B\u65F6\u95F4"),
					("{musictime:p}", "\u97F3\u4E50\u5DF2\u64AD\u653E\u65F6\u95F4"),
					("{datey}", "\u5F53\u524D\u5E74\u4EFD"),
					("{datem}", "\u5F53\u524D\u6708\u4EFD"),
					("{dated}", "\u5F53\u524D\u65E5\u671F"),
					("{wtime}", "\u7535\u8111\u65F6\u95F4\uFF0C24 \u5C0F\u65F6\u5236"),
					("{wtime12}", "\u7535\u8111\u65F6\u95F4\uFF0C12 \u5C0F\u65F6\u5236")
				});
				ImGui.EndTabItem();
			}

			if (ImGui.BeginTabItem("\u5224\u5B9A\u6210\u7EE9"))
			{
				DrawTagTable("JudgeTagTable", ovText, new (string Tag, string Desc)[]
				{
					("{judge}", "\u5F53\u524D\u5224\u5B9A\u6A21\u5F0F"),
					("{interval}", "\u5B9A\u65F6\u7A97\u53E3\u5927\u5C0F\u767E\u5206\u6BD4"),
					("{acc}", "\u51C6\u786E\u7387\uFF0C\u6700\u591A 2 \u4F4D\u5C0F\u6570"),
					("{acc:2}", "\u51C6\u786E\u7387\uFF0C\u6700\u591A 2 \u4F4D\u5C0F\u6570"),
					("{xacc}", "X-Accuracy\uFF0C\u6700\u591A 2 \u4F4D\u5C0F\u6570"),
					("{xacc:2}", "X-Accuracy\uFF0C\u6700\u591A 2 \u4F4D\u5C0F\u6570"),
					("{progress}", "\u5730\u56FE\u8FDB\u5EA6\uFF0C\u6700\u591A 2 \u4F4D\u5C0F\u6570"),
					("{progress:2}", "\u5730\u56FE\u8FDB\u5EA6\uFF0C\u6700\u591A 2 \u4F4D\u5C0F\u6570")
				});
				ImGui.EndTabItem();
			}

			if (ImGui.BeginTabItem("\u5224\u5B9A\u6570\u91CF"))
			{
				DrawTagTable("HitTagTable", ovText, new (string Tag, string Desc)[]
				{
					("{te}", "Too Early \u6570\u91CF"),
					("{ve}", "Very Early \u6570\u91CF"),
					("{ep}", "Early Perfect \u6570\u91CF"),
					("{p}", "Pure Perfect \u6570\u91CF"),
					("{lp}", "Late Perfect \u6570\u91CF"),
					("{vl}", "Very Late \u6570\u91CF"),
					("{tl}", "Too Late \u6570\u91CF"),
					("{fm}", "\u9519\u8FC7\u6570\u91CF"),
					("{fo}", "\u6309\u592A\u5FEB\u6570\u91CF"),
					("{miss}", "\u9519\u8FC7 + \u6309\u592A\u5FEB\u5408\u8BA1")
				});
				ImGui.EndTabItem();
			}

			if (ImGui.BeginTabItem("\u6837\u5F0F"))
			{
				DrawStyleTagTable("StyleTagTable", ovText, new (string Tag, string Desc)[]
				{
					("<color=#D4D4D6FF></color>", "\u5E26\u900F\u660E\u5EA6\u7684\u6587\u5B57\u989C\u8272"),
					("<color=#FFFFFFFF></color>", "\u767D\u8272\uFF0C\u5E26\u900F\u660E\u5EA6"),
					("<size=150%></size>", "\u76F8\u5BF9\u5B57\u53F7"),
					("<size=32></size>", "\u7EDD\u5BF9\u5B57\u53F7")
				});
				ImGui.EndTabItem();
			}

			ImGui.EndTabBar();
		}

		ImGui.EndPopup();
	}

	private void DrawTagTable(string tableId, OverlayerText ovText, (string Tag, string Desc)[] rows)
	{
		if (!ImGui.BeginTable(tableId, 3, ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.RowBg))
		{
			return;
		}

		ImGui.TableSetupColumn("\u6807\u7B7E", ImGuiTableColumnFlags.WidthFixed, 120f);
		ImGui.TableSetupColumn("\u8BF4\u660E", ImGuiTableColumnFlags.WidthStretch);
		ImGui.TableSetupColumn("\u64CD\u4F5C", ImGuiTableColumnFlags.WidthFixed, 72f);
		ImGui.TableHeadersRow();

		foreach ((string tag, string desc) in rows)
		{
			ImGui.TableNextRow();
			ImGui.TableNextColumn();
			ImGui.Text(tag);
			ImGui.TableNextColumn();
			ImGui.TextWrapped(desc);
			ImGui.TableNextColumn();
			if (ImGui.Button($"\u63D2\u5165##{tableId}_{tag}"))
			{
				InsertTagToFormat(ovText, tag, false);
			}
		}

		ImGui.EndTable();
	}

	private void DrawStyleTagTable(string tableId, OverlayerText ovText, (string Tag, string Desc)[] rows)
	{
		if (!ImGui.BeginTable(tableId, 3, ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.RowBg))
		{
			return;
		}

		ImGui.TableSetupColumn("\u6807\u7B7E", ImGuiTableColumnFlags.WidthFixed, 230f);
		ImGui.TableSetupColumn("\u8BF4\u660E", ImGuiTableColumnFlags.WidthStretch);
		ImGui.TableSetupColumn("\u64CD\u4F5C", ImGuiTableColumnFlags.WidthFixed, 72f);
		ImGui.TableHeadersRow();

		foreach ((string tag, string desc) in rows)
		{
			ImGui.TableNextRow();
			ImGui.TableNextColumn();
			ImGui.Text(tag);
			ImGui.TableNextColumn();
			ImGui.TextWrapped(desc);
			ImGui.TableNextColumn();
			if (ImGui.Button($"\u63D2\u5165##{tableId}_{tag}"))
			{
				InsertTagToFormat(ovText, tag, true);
			}
		}

		ImGui.EndTable();
	}

	private static bool GetHitTextHidden(HitMargin margin)
	{
		if (Main.Settings == null) return false;
		switch (margin)
		{
			case HitMargin.TooEarly: return Main.Settings.HideHitTextTooEarly;
			case HitMargin.VeryEarly: return Main.Settings.HideHitTextVeryEarly;
			case HitMargin.EarlyPerfect: return Main.Settings.HideHitTextEarlyPerfect;
			case HitMargin.Perfect: return Main.Settings.HideHitTextPerfect;
			case HitMargin.LatePerfect: return Main.Settings.HideHitTextLatePerfect;
			case HitMargin.VeryLate: return Main.Settings.HideHitTextVeryLate;
			case HitMargin.TooLate: return Main.Settings.HideHitTextTooLate;
			case HitMargin.Multipress: return Main.Settings.HideHitTextMultipress;
			case HitMargin.FailMiss: return Main.Settings.HideHitTextFailMiss;
			case HitMargin.FailOverload: return Main.Settings.HideHitTextFailOverload;
			case HitMargin.OverPress: return Main.Settings.HideHitTextOverPress;
			default: return false;
		}
	}

	private static void SetHitTextHidden(HitMargin margin, bool value)
	{
		if (Main.Settings == null) return;
		switch (margin)
		{
			case HitMargin.TooEarly: Main.Settings.HideHitTextTooEarly = value; break;
			case HitMargin.VeryEarly: Main.Settings.HideHitTextVeryEarly = value; break;
			case HitMargin.EarlyPerfect: Main.Settings.HideHitTextEarlyPerfect = value; break;
			case HitMargin.Perfect: Main.Settings.HideHitTextPerfect = value; break;
			case HitMargin.LatePerfect: Main.Settings.HideHitTextLatePerfect = value; break;
			case HitMargin.VeryLate: Main.Settings.HideHitTextVeryLate = value; break;
			case HitMargin.TooLate: Main.Settings.HideHitTextTooLate = value; break;
			case HitMargin.Multipress: Main.Settings.HideHitTextMultipress = value; break;
			case HitMargin.FailMiss: Main.Settings.HideHitTextFailMiss = value; break;
			case HitMargin.FailOverload: Main.Settings.HideHitTextFailOverload = value; break;
			case HitMargin.OverPress: Main.Settings.HideHitTextOverPress = value; break;
		}
	}

	private bool DrawHitTextToggle(string label, HitMargin margin)
	{
		bool value = GetHitTextHidden(margin);
		if (ImGui.Checkbox(label, ref value))
		{
			SetHitTextHidden(margin, value);
			Main.RequestSave();
			return true;
		}
		return false;
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

	private static string FormatPreviewDuration(double seconds)
	{
		if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0.0)
		{
			seconds = 0.0;
		}

		int totalSeconds = (int)Math.Floor(seconds);
		int hours = totalSeconds / 3600;
		int minutes = (totalSeconds / 60) % 60;
		int secs = totalSeconds % 60;

		if (hours > 0)
		{
			return string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}:{2:00}", hours, minutes, secs);
		}

		return string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}", minutes, secs);
	}

	private static string FormatPreviewNumber(double value, Match match, int defaultDecimals)
	{
		int decimals = defaultDecimals;
		if (match.Groups[1].Success && !int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out decimals))
		{
			decimals = defaultDecimals;
		}

		decimals = Math.Max(0, Math.Min(6, decimals));
		if (double.IsNaN(value) || double.IsInfinity(value))
		{
			value = 0.0;
		}

		double rounded = Math.Round(value, decimals, MidpointRounding.AwayFromZero);
		if (Math.Abs(rounded) < Math.Pow(10.0, -decimals) * 0.5)
		{
			rounded = 0.0;
		}

		if (decimals == 0)
		{
			return rounded.ToString("0", CultureInfo.InvariantCulture);
		}

		return rounded.ToString("0." + new string('#', decimals), CultureInfo.InvariantCulture);
	}

	private static string FormatPreviewTags(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return text;
		}

		int kps = (Object)KeyViewerManager.Instance != (Object)null ? KeyViewerManager.Instance.CurrentKPS : 0;
		int totalHits = Main.Settings != null ? Main.Settings.TotalHits : 0;
		DateTime now = DateTime.Now;

		string result = text
			.Replace("{kps}", kps.ToString(CultureInfo.InvariantCulture))
			.Replace("{tot}", totalHits.ToString(CultureInfo.InvariantCulture))
			.Replace("{ttile}", "128")
			.Replace("{atile}", "65")
			.Replace("{bpm}", "120")
			.Replace("{tbpm}", "120")
			.Replace("{cbpm}", "120")
			.Replace("{x}", "1.00")
			.Replace("{cur}", "2.00")
			.Replace("{maptime}", FormatPreviewDuration(150.0))
			.Replace("{maptime:p}", FormatPreviewDuration(45.0))
			.Replace("{musictime}", FormatPreviewDuration(180.0))
			.Replace("{musictime:p}", FormatPreviewDuration(60.0))
			.Replace("{judge}", "\u666E\u901A")
			.Replace("{interval}", "100%")
			.Replace("{datey}", now.ToString("yyyy", CultureInfo.InvariantCulture))
			.Replace("{datem}", now.ToString("MM", CultureInfo.InvariantCulture))
			.Replace("{dated}", now.ToString("dd", CultureInfo.InvariantCulture))
			.Replace("{wtime12}", now.ToString("hh:mm:ss tt", CultureInfo.InvariantCulture))
			.Replace("{wtime}", now.ToString("HH:mm:ss", CultureInfo.InvariantCulture))
			.Replace("{te}", "1")
			.Replace("{ve}", "2")
			.Replace("{ep}", "3")
			.Replace("{p}", "45")
			.Replace("{lp}", "3")
			.Replace("{vl}", "2")
			.Replace("{tl}", "1")
			.Replace("{fm}", "0")
			.Replace("{fo}", "0")
			.Replace("{miss}", "0")
			.Replace("{combo:p}", "12")
			.Replace("{combo}", "34")
			.Replace("{music}", "Artist - SongName")
			.Replace("{level}", "Level Author");

		result = Regex.Replace(result, @"\{fps(?:[:](\d+))?\}", match => FormatPreviewNumber(144.0, match, 0));
		result = Regex.Replace(result, @"\{acc(?:[:](\d+))?\}", match => FormatPreviewNumber(98.5, match, 2));
		result = Regex.Replace(result, @"\{xacc(?:[:](\d+))?\}", match => FormatPreviewNumber(97.0, match, 2));
		result = Regex.Replace(result, @"\{progress(?:[:](\d+))?\}", match => FormatPreviewNumber(50.0, match, 2));
		return result;
	}

	private static void ReloadSettingsAfterImport(string sourcePath)
	{
		Main.Settings = UnityModManager.ModSettings.Load<Settings>(Main.ModEntry);
		Main.Settings.InitNulls();
		if (CheryToolsAssets.ImportSettingsAssets(Main.Settings))
		{
			Main.Settings.Save(Main.ModEntry);
		}
		if ((Object)KeyViewerManager.Instance != (Object)null)
		{
			KeyViewerManager.Instance.RefreshKeys();
		}
		InputInterceptor.UpdateAllowedKeys();
		ImGuiController.NeedsFontAtlasRebuild = true;
		Main.Logger.Log("Settings imported successfully from: " + sourcePath);
	}

	private void ImportLegacyKeyViewerFromXml(string sourcePath)
	{
		ImportLegacyKeyViewer(sourcePath, false);
	}

	private void ImportLegacyKeyViewerFromCyt(string sourcePath)
	{
		ImportLegacyKeyViewer(sourcePath, true);
	}

	private void ImportLegacyKeyViewer(string sourcePath, bool isCyt)
	{
		try
		{
			string message;
			int imported = isCyt
				? LegacyKeyViewerImporter.ImportFromCytPackage(Main.Settings, sourcePath, out message)
				: LegacyKeyViewerImporter.ImportFromXmlFile(Main.Settings, sourcePath, out message);

			_legacyKeyViewerImportMessage = message;
			if (imported <= 0)
			{
				Main.Logger.Log("Legacy KeyViewer import skipped: " + message);
				return;
			}

			if (CheryToolsAssets.ImportSettingsAssets(Main.Settings))
			{
				_legacyKeyViewerImportMessage += " 已同步外置资源。";
			}

			InputInterceptor.UpdateAllowedKeys();
			TextureManager.Clear();
			ImGuiController.NeedsFontAtlasRebuild = true;
			Main.Settings.Save(Main.ModEntry);
			Main.Logger.Log("Legacy KeyViewer imported from: " + sourcePath);
		}
		catch (Exception ex)
		{
			_legacyKeyViewerImportMessage = "旧 KV 导入失败: " + ex.Message;
			Main.Logger.Log("Failed to import legacy KeyViewer settings: " + ex.ToString());
		}
	}

	private void ExportKeyViewerPackage()
	{
		try
		{
			string path = CheryToolsAssets.ExportKeyViewerPackage(Main.Settings);
			_keyViewerExportMessage = "已导出: " + path;
			Main.Logger.Log("KeyViewer package exported to: " + path);
		}
		catch (Exception ex)
		{
			_keyViewerExportMessage = "KV 导出失败: " + ex.Message;
			Main.Logger.Log("Failed to export KeyViewer package: " + ex.ToString());
		}
	}

	private void ImportKeyViewerPackage()
	{
		try
		{
			string path = Path.Combine(CheryToolsAssets.GameRoot, "CheryTools_KeyViewer.ctkv");
			CheryToolsAssets.ImportKeyViewerPackage(Main.Settings, path);
			Main.Settings.InitNulls();
			if (CheryToolsAssets.ImportSettingsAssets(Main.Settings))
			{
				_keyViewerExportMessage = "已导入并同步外置资源: " + path;
			}
			else
			{
				_keyViewerExportMessage = "已导入: " + path;
			}
			_selectedKVSidebarTab = Main.Settings.KeyViewerSelectedConfigIndex;
			InputInterceptor.UpdateAllowedKeys();
			TextureManager.Clear();
			ImGuiController.NeedsFontAtlasRebuild = true;
			Main.Settings.Save(Main.ModEntry);
			Main.Logger.Log("KeyViewer package imported from: " + path);
		}
		catch (Exception ex)
		{
			_keyViewerExportMessage = "KV 导入失败: " + ex.Message;
			Main.Logger.Log("Failed to import KeyViewer package: " + ex.ToString());
		}
	}

	private void ExportOverlayerPackage()
	{
		try
		{
			string path = CheryToolsAssets.ExportOverlayerPackage(Main.Settings);
			_overlayerExportMessage = "已导出: " + path;
			Main.Logger.Log("Overlayer package exported to: " + path);
		}
		catch (Exception ex)
		{
			_overlayerExportMessage = "OV 导出失败: " + ex.Message;
			Main.Logger.Log("Failed to export Overlayer package: " + ex.ToString());
		}
	}

	private void ImportOverlayerPackage()
	{
		try
		{
			string path = Path.Combine(CheryToolsAssets.GameRoot, "CheryTools_Overlayer.ctov");
			CheryToolsAssets.ImportOverlayerPackage(Main.Settings, path);
			Main.Settings.InitNulls();
			if (CheryToolsAssets.ImportSettingsAssets(Main.Settings))
			{
				_overlayerExportMessage = "已导入并同步外置资源: " + path;
			}
			else
			{
				_overlayerExportMessage = "已导入: " + path;
			}
			_selectedOvSidebarTab = 0;
			_selectedOvSidebarImgTab = 0;
			_selectedOvSidebarBarTab = 0;
			TextureManager.Clear();
			SdfTextRenderer.Shutdown();
			ImGuiController.NeedsFontAtlasRebuild = true;
			Main.Settings.Save(Main.ModEntry);
			Main.Logger.Log("Overlayer package imported from: " + path);
		}
		catch (Exception ex)
		{
			_overlayerExportMessage = "OV 导入失败: " + ex.Message;
			Main.Logger.Log("Failed to import Overlayer package: " + ex.ToString());
		}
	}


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

	private bool _waitingForToggleMenuKey = false;

	private bool _editingImGuiPanelScale = false;

	private float _pendingImGuiPanelScale = 1.0f;

	private bool _editingOverlayUpdateRate = false;

	private float _pendingOverlayUpdateRate = 240.0f;

	private bool _editingImageRenderScale = false;

	private float _pendingImageRenderScale = 1.0f;

	private string _gameUIDeveloperKeyInput = "";

	private bool _gameUIDeveloperKeyFailed = false;

	private string _legacyKeyViewerImportMessage = "";

	private string _keyViewerExportMessage = "";

	private string _overlayerExportMessage = "";

	

	private int _selectedKVSidebarTab = -1;

	private static int _selectedOvSidebarTab = 0;

	private static int _selectedOvSidebarImgTab = 0;

	private static int _selectedOvSidebarBarTab = 0;

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

	private const string ProgressValueSourceCombo =
		"常量\0" +
		"地图进度 {progress}\0" +
		"准度 {acc}\0" +
		"X 准度 {xacc}\0" +
		"KPS {kps}\0" +
		"当前真实 CPS {cur}\0" +
		"地图已游玩时间 {maptime:p}\0" +
		"地图总时间 {maptime}\0" +
		"音乐已播放时间 {musictime:p}\0" +
		"音乐总时间 {musictime}\0" +
		"Pure Combo {combo}\0" +
		"Perfect Combo {combo:p}\0" +
		"死亡数 {miss}\0" +
		"错过 {fm}\0" +
		"按太快 {fo}\0";

	private bool DrawProgressValueSourceEditor(string label, string id, OverlayerProgressValueSource source)
	{
		if (source == null)
		{
			return false;
		}

		bool changed = false;
		int kind = (int)source.Kind;
		if (kind < 0 || kind > (int)OverlayerProgressValueKind.FailOverload)
		{
			kind = 0;
		}

		ImGui.SetNextItemWidth(260f);
		if (ImGui.Combo(label + "##" + id, ref kind, ProgressValueSourceCombo))
		{
			source.Kind = (OverlayerProgressValueKind)kind;
			changed = true;
		}

		if (source.Kind == OverlayerProgressValueKind.Constant)
		{
			float constant = (float)source.Constant;
			ImGui.SetNextItemWidth(140f);
			if (ImGui.DragFloat("常量值##" + id + "_constant", ref constant, 0.1f, -100000f, 100000f, "%.2f"))
			{
				source.Constant = constant;
				changed = true;
			}
		}

		return changed;
	}

	private void DrawGameUISettings()
	{
		Main.Settings.EnsureGameUIElementSettings();

		ImGui.Text("\u6E38\u620F UI");
		ImGui.Separator();
		ImGui.TextWrapped("\u4EC5\u5728\u5173\u5361\u6E38\u73A9\u754C\u9762\u751F\u6548\uFF0C\u4E0D\u63A5\u7BA1\u83DC\u5355\u3001\u9009\u5173\u6216\u5173\u5361\u7F16\u8F91\u5668 UI\u3002");

		bool enabled = Main.Settings.GameUIControlEnabled;
		if (ImGui.Checkbox("\u542F\u7528\u6E38\u620F UI \u63A7\u5236", ref enabled))
		{
			Main.Settings.GameUIControlEnabled = enabled;
			if (!enabled && GameUIManager.Instance != null)
			{
				GameUIManager.Instance.RestoreAll();
			}
			Main.RequestSave();
		}

		ImGui.SameLine();
		if (ImGui.Button("\u91CD\u7F6E\u5168\u90E8##GameUIResetAll"))
		{
			Main.Settings.ResetGameUIElementSettings();
			if (GameUIManager.Instance != null)
			{
				GameUIManager.Instance.RestoreAll();
			}
			Main.RequestSave();
		}

		ImGui.Separator();

		foreach (var target in GameUIManager.Targets)
		{
			GameUIElementSetting setting = Main.Settings.GetGameUIElement(target.Id);
			if (setting == null)
			{
				continue;
			}

			ImGui.PushID("game_ui_" + target.Id);
			if (ImGui.CollapsingHeader(target.DisplayName + "##header"))
			{
				bool controlled = setting.Enabled;
				if (ImGui.Checkbox("\u63A5\u7BA1", ref controlled))
				{
					setting.Enabled = controlled;
					if (!controlled && GameUIManager.Instance != null)
					{
						GameUIManager.Instance.RestoreTarget(target.Id);
					}
					Main.RequestSave();
				}

				ImGui.SameLine();
				bool visible = setting.Visible;
				if (ImGui.Checkbox("\u53EF\u89C1", ref visible))
				{
					setting.Visible = visible;
					Main.RequestSave();
				}

				bool advancedLocked = GameUIManager.IsRestrictedAdvancedTarget(target.Id) && !Main.Settings.GameUIDeveloperUnlocked;
				if (advancedLocked)
				{
					ImGui.TextColored(new System.Numerics.Vector4(1f, 0.78f, 0.35f, 1f), "\u5F00\u53D1\u8005\u9009\u9879\u672A\u89E3\u9501\uFF1A\u6B64\u9879\u4EC5\u652F\u6301\u9690\u85CF\uFF0C\u4F4D\u7F6E/\u7F29\u653E/\u900F\u660E\u5EA6\u5DF2\u7981\u7528\u3002");
					ImGui.BeginDisabled();
				}

				float offsetX = setting.OffsetX;
				ImGui.SetNextItemWidth(120f);
				if (ImGui.DragFloat("\u4F4D\u7F6E X", ref offsetX, 1f, -3000f, 3000f, "%.1f"))
				{
					setting.OffsetX = offsetX;
					Main.RequestSave();
				}

				ImGui.SameLine();
				float offsetY = setting.OffsetY;
				ImGui.SetNextItemWidth(120f);
				if (ImGui.DragFloat("\u4F4D\u7F6E Y", ref offsetY, 1f, -3000f, 3000f, "%.1f"))
				{
					setting.OffsetY = offsetY;
					Main.RequestSave();
				}

				float scale = setting.Scale;
				ImGui.SetNextItemWidth(160f);
				if (ImGui.SliderFloat("\u7F29\u653E", ref scale, 0.05f, 5f, "%.2f"))
				{
					setting.Scale = Math.Max(0.05f, Math.Min(5f, scale));
					Main.RequestSave();
				}

				ImGui.SameLine();
				float alpha = setting.Alpha;
				ImGui.SetNextItemWidth(160f);
				if (ImGui.SliderFloat("\u900F\u660E\u5EA6", ref alpha, 0f, 1f, "%.2f"))
				{
					setting.Alpha = Math.Max(0f, Math.Min(1f, alpha));
					Main.RequestSave();
				}

				if (advancedLocked)
				{
					ImGui.EndDisabled();
				}

				if (ImGui.Button("\u91CD\u7F6E\u6B64\u9879##reset"))
				{
					setting.Enabled = false;
					setting.Visible = true;
					setting.OffsetX = 0f;
					setting.OffsetY = 0f;
					setting.Scale = 1f;
					setting.Alpha = 1f;
					if (GameUIManager.Instance != null)
					{
						GameUIManager.Instance.RestoreTarget(target.Id);
					}
					Main.RequestSave();
				}
			}
			ImGui.PopID();
		}
	}

	private void AddKeyViewerConfiguration(string name, int presetKeyCount)
	{
		Main.Settings.EnsureKeyViewerConfigurations();
		KVConfiguration config = Main.Settings.CreateKeyViewerConfiguration(name, presetKeyCount);
		Main.Settings.KeyViewerConfigurations.Add(config);
		Main.Settings.KeyViewerSelectedConfigIndex = Main.Settings.KeyViewerConfigurations.Count - 1;
		_selectedKVSidebarTab = Main.Settings.KeyViewerSelectedConfigIndex;
		InputInterceptor.UpdateAllowedKeys();
		Main.RequestSave();
	}

	private void DrawKeyViewerConfigurations()
	{
		Main.Settings.EnsureKeyViewerConfigurations();
		List<KVConfiguration> configs = Main.Settings.KeyViewerConfigurations;
		_selectedKVSidebarTab = Main.Settings.KeyViewerSelectedConfigIndex;

		ImGui.BeginChild("KVConfigSidebar", new Vector2(150f, 0f), ImGuiChildFlags.Borders);
		if (ImGui.Button("新建配置", new Vector2(-1f, 0f)))
		{
			ImGui.OpenPopup("KVCreateConfigPopup");
		}
		if (ImGui.BeginPopup("KVCreateConfigPopup"))
		{
			if (ImGui.MenuItem("空白配置")) AddKeyViewerConfiguration("空白配置 " + (configs.Count + 1).ToString(), 0);
			ImGui.Separator();
			if (ImGui.MenuItem("从 16K 预设开始")) AddKeyViewerConfiguration("16K 配置 " + (configs.Count + 1).ToString(), 16);
			if (ImGui.MenuItem("从 12K 预设开始")) AddKeyViewerConfiguration("12K 配置 " + (configs.Count + 1).ToString(), 12);
			if (ImGui.MenuItem("从 10K 预设开始")) AddKeyViewerConfiguration("10K 配置 " + (configs.Count + 1).ToString(), 10);
			if (ImGui.MenuItem("从 8K 预设开始")) AddKeyViewerConfiguration("8K 配置 " + (configs.Count + 1).ToString(), 8);
			ImGui.EndPopup();
		}
		ImGui.Separator();

		for (int i = 0; i < configs.Count; i++)
		{
			KVConfiguration config = configs[i];
			if (config == null) continue;
			ImGui.PushID("kv_cfg_sidebar_" + i.ToString());
			bool enabled = config.IsEnabled;
			if (ImGui.Checkbox("##enabled", ref enabled))
			{
				config.IsEnabled = enabled;
				InputInterceptor.UpdateAllowedKeys();
				Main.RequestSave();
			}
			ImGui.SameLine();
			string configName = string.IsNullOrEmpty(config.Name) ? "KV 配置 " + (i + 1).ToString() : config.Name;
			if (ImGui.Selectable(configName, Main.Settings.KeyViewerSelectedConfigIndex == i))
			{
				Main.Settings.KeyViewerSelectedConfigIndex = i;
				_selectedKVSidebarTab = i;
				Main.RequestSave();
			}
			ImGui.PopID();
		}
		ImGui.EndChild();
		ImGui.SameLine();

		ImGui.BeginChild("KVConfigContent", new Vector2(0f, 0f), ImGuiChildFlags.Borders);
		KVConfiguration selectedConfig = Main.Settings.GetSelectedKeyViewerConfiguration();
		if (selectedConfig == null)
		{
			ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1f), "还没有 KV 配置");
			ImGui.Text("点击左侧“新建配置”开始。");
			ImGui.EndChild();
			return;
		}

		DrawKeyViewerSelectedConfiguration(selectedConfig);
		ImGui.Separator();
		DrawKeyViewerConfigSettings(selectedConfig);
		ImGui.EndChild();
	}

	private void DrawKeyViewerSelectedConfiguration(KVConfiguration config)
	{
		ImGui.Text("当前配置");
		ImGui.Separator();

		string name = config.Name ?? "";
		if (ImGui.InputText("名称##kv_config_name", ref name, 128u))
		{
			config.Name = name;
			Main.RequestSave();
		}

		bool enabled = config.IsEnabled;
		if (ImGui.Checkbox("启用显示##kv_config_enabled", ref enabled))
		{
			config.IsEnabled = enabled;
			InputInterceptor.UpdateAllowedKeys();
			Main.RequestSave();
		}

		int nodeCount = config.Nodes != null ? config.Nodes.Count : 0;
		ImGui.Text("节点数量: " + nodeCount.ToString());

		if (ImGui.Button("打开 FreeMake 编辑器", new Vector2(200f, 30f)))
		{
			FreeMakeEditor.IsOpen = true;
		}
		ImGui.SameLine();
		if (ImGui.Button("清空节点", new Vector2(90f, 30f)))
		{
			if (config.Nodes != null) config.Nodes.Clear();
			InputInterceptor.UpdateAllowedKeys();
			Main.RequestSave();
		}

		ImGui.SameLine();
		ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.8f, 0.2f, 0.2f, 1f));
		bool deletePressed = ImGui.Button("删除配置", new Vector2(90f, 30f));
		ImGui.PopStyleColor();
		if (deletePressed)
		{
			int index = Main.Settings.KeyViewerSelectedConfigIndex;
			if (index >= 0 && index < Main.Settings.KeyViewerConfigurations.Count)
			{
				Main.Settings.KeyViewerConfigurations.RemoveAt(index);
				Main.Settings.KeyViewerSelectedConfigIndex = Math.Min(index, Main.Settings.KeyViewerConfigurations.Count - 1);
				if (Main.Settings.KeyViewerSelectedConfigIndex < 0 && Main.Settings.KeyViewerConfigurations.Count > 0)
					Main.Settings.KeyViewerSelectedConfigIndex = 0;
				_selectedKVSidebarTab = Main.Settings.KeyViewerSelectedConfigIndex;
				InputInterceptor.UpdateAllowedKeys();
				Main.RequestSave();
			}
			return;
		}

		ImGui.Text("用预设覆盖当前配置:");
		if (ImGui.Button("16K##kv_cfg_reset_16")) ReplaceSelectedKeyViewerConfig(config, 16);
		ImGui.SameLine();
		if (ImGui.Button("12K##kv_cfg_reset_12")) ReplaceSelectedKeyViewerConfig(config, 12);
		ImGui.SameLine();
		if (ImGui.Button("10K##kv_cfg_reset_10")) ReplaceSelectedKeyViewerConfig(config, 10);
		ImGui.SameLine();
		if (ImGui.Button("8K##kv_cfg_reset_8")) ReplaceSelectedKeyViewerConfig(config, 8);
		ImGui.SameLine();
		if (ImGui.Button("空白##kv_cfg_reset_empty")) ReplaceSelectedKeyViewerConfig(config, 0);
	}

	private void ReplaceSelectedKeyViewerConfig(KVConfiguration config, int presetKeyCount)
	{
		if (config == null) return;
		config.Nodes = presetKeyCount > 0
			? Main.Settings.GenerateDefaultKVLayout(presetKeyCount, config.DefaultWidth, config.DefaultHeight)
			: new List<KVNode>();
		InputInterceptor.UpdateAllowedKeys();
		if ((Object)KeyViewerManager.Instance != (Object)null)
		{
			KeyViewerManager.Instance.ResetCounts();
		}
		Main.RequestSave();
	}

	private void DrawKeyViewerConfigSettings(KVConfiguration config)
	{
		if (config == null) return;

		List<KVNode> selectedNodes = config.Nodes;
		ImGui.Text("当前配置外观设置");
		ImGui.Separator();
		string input = config.FontPath ?? "";
		if (ImGui.InputText("自定义字体路径 (如 D:/font.ttf 或 .otf)", ref input, 256u))
		{
			config.FontPath = input;
			Main.RequestSave();
		}
		if (ImGui.IsItemDeactivatedAfterEdit())
		{
			ImportResourcePath(ref config.FontPath, "Fonts", true);
		}
		float textOffsetX = config.GlobalTextOffsetX;
		if (ImGui.DragFloat("配置按键文字X偏移", ref textOffsetX, 1f))
		{
			config.GlobalTextOffsetX = textOffsetX;
			Main.RequestSave();
		}
		float textOffsetY = config.GlobalTextOffsetY;
		if (ImGui.DragFloat("配置按键文字Y偏移", ref textOffsetY, 1f))
		{
			config.GlobalTextOffsetY = textOffsetY;
			Main.RequestSave();
		}
		float countOffsetX = config.GlobalCountOffsetX;
		if (ImGui.DragFloat("配置计数文字X偏移", ref countOffsetX, 1f))
		{
			config.GlobalCountOffsetX = countOffsetX;
			Main.RequestSave();
		}
		float countOffsetY = config.GlobalCountOffsetY;
		if (ImGui.DragFloat("配置计数文字Y偏移", ref countOffsetY, 1f))
		{
			config.GlobalCountOffsetY = countOffsetY;
			Main.RequestSave();
		}
		if (ImGui.Button("重新加载字体"))
		{
			ImportResourcePath(ref config.FontPath, "Fonts", true);
			ImGuiController.NeedsFontAtlasRebuild = true;
		}
		float scale = config.Scale;
		if (ImGui.SliderFloat("缩放大小", ref scale, 0.5f, 3f))
		{
			config.Scale = scale;
			Main.RequestSave();
		}
		bool hideCountText = config.HideCountText;
		if (ImGui.Checkbox("隐藏计数数字##kv_config_hide_count_text", ref hideCountText))
		{
			config.HideCountText = hideCountText;
			Main.RequestSave();
		}
		float borderThickness = config.BorderThickness;
		if (ImGui.SliderFloat("配置默认边框粗细", ref borderThickness, 0f, 5f))
		{
			config.BorderThickness = borderThickness;
			Main.RequestSave();
		}
		ImGui.Spacing();
		ImGui.Text("文字描边");
		bool keyTextOutlineEnabled = config.KeyTextOutlineEnabled;
		if (ImGui.Checkbox("开启按键文字描边##kv_config_key_outline", ref keyTextOutlineEnabled))
		{
			config.KeyTextOutlineEnabled = keyTextOutlineEnabled;
			Main.RequestSave();
		}
		if (keyTextOutlineEnabled)
		{
			ImGui.Indent();
			if (DrawColorPicker("按键文字描边颜色##kv_config_key_outline_color", ref config.KeyTextOutlineColor))
			{
				Main.RequestSave();
			}
			float keyTextOutlineThickness = config.KeyTextOutlineThickness;
			if (ImGui.DragFloat("按键文字描边粗细##kv_config_key_outline_thickness", ref keyTextOutlineThickness, 0.1f, 0f, 8f, "%.1f"))
			{
				config.KeyTextOutlineThickness = keyTextOutlineThickness;
				Main.RequestSave();
			}
			ImGui.Unindent();
		}
		bool countTextOutlineEnabled = config.CountTextOutlineEnabled;
		if (ImGui.Checkbox("开启计数文字描边##kv_config_count_outline", ref countTextOutlineEnabled))
		{
			config.CountTextOutlineEnabled = countTextOutlineEnabled;
			Main.RequestSave();
		}
		if (countTextOutlineEnabled)
		{
			ImGui.Indent();
			if (DrawColorPicker("计数文字描边颜色##kv_config_count_outline_color", ref config.CountTextOutlineColor))
			{
				Main.RequestSave();
			}
			float countTextOutlineThickness = config.CountTextOutlineThickness;
			if (ImGui.DragFloat("计数文字描边粗细##kv_config_count_outline_thickness", ref countTextOutlineThickness, 0.1f, 0f, 8f, "%.1f"))
			{
				config.CountTextOutlineThickness = countTextOutlineThickness;
				Main.RequestSave();
			}
			ImGui.Unindent();
		}
		ImGui.Spacing();
		ImGui.Text("批量修改当前配置按键大小");
		bool sizeChanged = false;
		float defaultWidth = config.DefaultWidth;
		if (ImGui.DragFloat("配置宽度设定##bulk_w", ref defaultWidth, 1f, 10f, 500f))
		{
			config.DefaultWidth = defaultWidth;
			sizeChanged = true;
		}
		float defaultHeight = config.DefaultHeight;
		if (ImGui.DragFloat("配置高度设定##bulk_h", ref defaultHeight, 1f, 10f, 500f))
		{
			config.DefaultHeight = defaultHeight;
			sizeChanged = true;
		}
		if (sizeChanged)
		{
			Main.RequestSave();
		}
		if (ImGui.Button("应用大小到当前配置的所有按键"))
		{
			if (selectedNodes != null)
			{
				foreach (KVNode item in selectedNodes)
				{
					if (item == null || item.NodeType != 0) continue;
					item.Width = config.DefaultWidth;
					item.Height = config.DefaultHeight;
				}
				Main.RequestSave();
			}
		}
		DrawKeyViewerColorAndRainSettings(config);
	}

	private void DrawKeyViewerColorAndRainSettings(KVConfiguration config)
	{
		if (config == null) return;

		ImGui.Spacing();
		ImGui.Text("颜色设置");
		ImGui.Separator();
		ImGui.Text("背景");
		if (DrawColorPicker("未按下##bg_norm", ref config.ColorBgNormal)) Main.RequestSave();
		ImGui.SameLine();
		if (DrawColorPicker("触发##bg_press", ref config.ColorBgPressed)) Main.RequestSave();
		ImGui.Text("边框");
		if (DrawColorPicker("未按下##border_norm", ref config.ColorBorderNormal)) Main.RequestSave();
		ImGui.SameLine();
		if (DrawColorPicker("触发##border_press", ref config.ColorBorderPressed)) Main.RequestSave();
		ImGui.Text("文本");
		if (DrawColorPicker("未按下##txt_norm", ref config.ColorTextNormal)) Main.RequestSave();
		ImGui.SameLine();
		if (DrawColorPicker("触发##txt_press", ref config.ColorTextPressed)) Main.RequestSave();
		ImGui.Text("底部统计文本");
		if (DrawColorPicker("KPS 颜色", ref config.ColorKps)) Main.RequestSave();
		ImGui.SameLine();
		if (DrawColorPicker("Total 颜色", ref config.ColorTotal)) Main.RequestSave();
		ImGui.Spacing();
		ImGui.Text("键雨 (Key Rain) 设置");
		ImGui.Separator();
		bool enableKeyRain = config.EnableKeyRain;
		if (ImGui.Checkbox("开启键雨##rain_enable", ref enableKeyRain))
		{
			config.EnableKeyRain = enableKeyRain;
			Main.RequestSave();
		}
		if (!enableKeyRain) return;

		float rainSpeed = config.KeyRainSpeed;
		if (ImGui.SliderFloat("飞行速度##rain_speed", ref rainSpeed, 100f, 2000f))
		{
			config.KeyRainSpeed = rainSpeed;
			Main.RequestSave();
		}
		float rainMaxHeight = config.KeyRainMaxHeight;
		if (ImGui.SliderFloat("消失距离##rain_maxh", ref rainMaxHeight, 100f, 1500f))
		{
			config.KeyRainMaxHeight = rainMaxHeight;
			Main.RequestSave();
		}
		float rainYOffsetRow1 = config.KeyRainYOffsetRow1;
		if (ImGui.SliderFloat("第一排高度偏移##rain_yoffset1", ref rainYOffsetRow1, -200f, 200f))
		{
			config.KeyRainYOffsetRow1 = rainYOffsetRow1;
			Main.RequestSave();
		}
		float rainYOffsetRow2 = config.KeyRainYOffsetRow2;
		if (ImGui.SliderFloat("第二排高度偏移##rain_yoffset2", ref rainYOffsetRow2, -200f, 200f))
		{
			config.KeyRainYOffsetRow2 = rainYOffsetRow2;
			Main.RequestSave();
		}
		int rainFadeMode = config.KeyRainFadeMode;
		if (ImGui.Combo("消失模式##rain_mode", ref rainFadeMode, "高度裁剪 (Clip)\0羽化透明 (Fade)\0"))
		{
			config.KeyRainFadeMode = rainFadeMode;
			Main.RequestSave();
		}
		float rainWidthRatio1 = config.KeyRainWidthRatio1;
		if (ImGui.SliderFloat("第一排宽度比例##rain_w1", ref rainWidthRatio1, 0.1f, 1f))
		{
			config.KeyRainWidthRatio1 = rainWidthRatio1;
			Main.RequestSave();
		}
		float rainWidthRatio2 = config.KeyRainWidthRatio2;
		if (ImGui.SliderFloat("第二排宽度比例##rain_w2", ref rainWidthRatio2, 0.1f, 1f))
		{
			config.KeyRainWidthRatio2 = rainWidthRatio2;
			Main.RequestSave();
		}
		ImGui.Text("雨滴颜色");
		if (DrawColorPicker("第一排颜色##rain_c1", ref config.KeyRainColorRow1)) Main.RequestSave();
		ImGui.SameLine();
		if (DrawColorPicker("第二排颜色##rain_c2", ref config.KeyRainColorRow2)) Main.RequestSave();
	}

	public unsafe void RenderUI()
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
			ImGui.SetNextWindowSize(new Vector2(620f, 430f), ImGuiCond.FirstUseEver);
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
				if (ImGui.Selectable("\u6E38\u620F UI", _currentToolTab == 2))
				{
					_currentToolTab = 2;
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
					bool disableAutoplaySpacePause = Main.Settings.DisableAutoplaySpacePause;
					if (ImGui.Checkbox("禁用自动播放空格暂停", ref disableAutoplaySpacePause))
					{
						Main.Settings.DisableAutoplaySpacePause = disableAutoplaySpacePause;
						Main.RequestSave();
					}
					bool disablePlayModeScrollZoom = Main.Settings.DisablePlayModeScrollZoom;
					if (ImGui.Checkbox("禁用播放时滚轮缩放", ref disablePlayModeScrollZoom))
					{
						Main.Settings.DisablePlayModeScrollZoom = disablePlayModeScrollZoom;
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
					
					
					ImGui.Separator();

					bool hideHitTextEnabled = Main.Settings.HideHitTextEnabled;
					if (ImGui.Checkbox("启用判定文字隐藏", ref hideHitTextEnabled))
					{
						Main.Settings.HideHitTextEnabled = hideHitTextEnabled;
						Main.RequestSave();
					}
					if (Main.Settings.HideHitTextEnabled)
					{
						if (ImGui.Button("隐藏全部判定文字"))
						{
							SetHitTextHidden(HitMargin.TooEarly, true);
							SetHitTextHidden(HitMargin.VeryEarly, true);
							SetHitTextHidden(HitMargin.EarlyPerfect, true);
							SetHitTextHidden(HitMargin.Perfect, true);
							SetHitTextHidden(HitMargin.LatePerfect, true);
							SetHitTextHidden(HitMargin.VeryLate, true);
							SetHitTextHidden(HitMargin.TooLate, true);
							SetHitTextHidden(HitMargin.Multipress, true);
							SetHitTextHidden(HitMargin.FailMiss, true);
							SetHitTextHidden(HitMargin.FailOverload, true);
							SetHitTextHidden(HitMargin.OverPress, true);
							Main.RequestSave();
						}
						ImGui.SameLine();
						if (ImGui.Button("显示全部判定文字"))
						{
							SetHitTextHidden(HitMargin.TooEarly, false);
							SetHitTextHidden(HitMargin.VeryEarly, false);
							SetHitTextHidden(HitMargin.EarlyPerfect, false);
							SetHitTextHidden(HitMargin.Perfect, false);
							SetHitTextHidden(HitMargin.LatePerfect, false);
							SetHitTextHidden(HitMargin.VeryLate, false);
							SetHitTextHidden(HitMargin.TooLate, false);
							SetHitTextHidden(HitMargin.Multipress, false);
							SetHitTextHidden(HitMargin.FailMiss, false);
							SetHitTextHidden(HitMargin.FailOverload, false);
							SetHitTextHidden(HitMargin.OverPress, false);
							Main.RequestSave();
						}
						DrawHitTextToggle("隐藏 Too Early", HitMargin.TooEarly);
						ImGui.SameLine();
						DrawHitTextToggle("隐藏 Very Early", HitMargin.VeryEarly);
						ImGui.SameLine();
						DrawHitTextToggle("隐藏 Early Perfect", HitMargin.EarlyPerfect);
						DrawHitTextToggle("隐藏 Perfect", HitMargin.Perfect);
						ImGui.SameLine();
						DrawHitTextToggle("隐藏 Late Perfect", HitMargin.LatePerfect);
						ImGui.SameLine();
						DrawHitTextToggle("隐藏 Very Late", HitMargin.VeryLate);
						DrawHitTextToggle("隐藏 Too Late", HitMargin.TooLate);
						ImGui.SameLine();
						DrawHitTextToggle("隐藏 多押", HitMargin.Multipress);
						ImGui.SameLine();
						DrawHitTextToggle("隐藏 OverPress", HitMargin.OverPress);
						DrawHitTextToggle("隐藏 错过", HitMargin.FailMiss);
						ImGui.SameLine();
						DrawHitTextToggle("隐藏 按太快", HitMargin.FailOverload);
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
				else if (_currentToolTab == 2)
				{
					DrawGameUISettings();
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
				if (ImGui.Button("导出 KV 配置 (.ctkv)"))
				{
					ExportKeyViewerPackage();
				}
				ImGui.SameLine();
				if (ImGui.Button("导入 KV 配置 (.ctkv)"))
				{
					ImportKeyViewerPackage();
				}
				if (!string.IsNullOrEmpty(_keyViewerExportMessage))
				{
					ImGui.TextColored(new System.Numerics.Vector4(0.75f, 0.85f, 1f, 1f), _keyViewerExportMessage);
				}
				ImGui.Separator();
				DrawKeyViewerConfigurations();
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
				if (ImGui.Button("导出 OV 配置 (.ctov)"))
				{
					ExportOverlayerPackage();
				}
				ImGui.SameLine();
				if (ImGui.Button("导入 OV 配置 (.ctov)"))
				{
					ImportOverlayerPackage();
				}
				if (!string.IsNullOrEmpty(_overlayerExportMessage))
				{
					ImGui.TextColored(new System.Numerics.Vector4(0.75f, 0.85f, 1f, 1f), _overlayerExportMessage);
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
									ImGui.AlignTextToFramePadding();
									ImGui.Text("\u516C\u5F0F");
									ImGui.SameLine();
									if (ImGui.Button("\u63D2\u5165 Tag (Insert Tag)"))
									{
										ImGui.OpenPopup("TagSelectorPopup");
									}
									DrawOverlayerTagInsertPopup(overlayerText2);

									string input3 = overlayerText2.TextFormat;
									if (ImGui.InputTextMultiline("##TextFormat", ref input3, 1024u, new Vector2(0f, 60f), ImGuiInputTextFlags.CallbackAlways, TextFormatCallback))
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

									int textDepth = RenderDepth.ClampDepth(overlayerText2.Depth);
									ImGui.SetNextItemWidth(180f);
									if (ImGui.SliderInt("\u6DF1\u5EA6##ovTextDepth", ref textDepth, RenderDepth.MinDepth, RenderDepth.MaxDepth))
									{
										overlayerText2.Depth = RenderDepth.ClampDepth(textDepth);
										Main.RequestSave();
									}

									float pX = overlayerText2.PivotX;
									float pY = overlayerText2.PivotY;
									ImGui.SetNextItemWidth(120f);
									if (ImGui.SliderFloat("锚点 X (Pivot)##pivotX", ref pX, 0f, 1f, "%.2f"))
									{
										overlayerText2.PivotX = pX;
										Main.RequestSave();
									}
									ImGui.SameLine();
									ImGui.SetNextItemWidth(120f);
									if (ImGui.SliderFloat("锚点 Y (Pivot)##pivotY", ref pY, 0f, 1f, "%.2f"))
									{
										overlayerText2.PivotY = pY;
										Main.RequestSave();
									}

									ImGui.Text("快速对齐:");
									ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(2f, 2f));
									ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(4f, 4f));
									
									Vector2 btnSize = new Vector2(28f, 28f);
									Vector2 displaySize = ImGuiController.ScreenDisplaySize;
									
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
										float shadowSoftness = overlayerText2.ShadowSoftness;
										if (ImGui.DragFloat("阴影柔度##textShadowSoftness", ref shadowSoftness, 0.5f, 0f, 64f, "%.1f"))
										{
											overlayerText2.ShadowSoftness = Math.Max(0f, shadowSoftness);
											Main.RequestSave();
										}
										ImGui.Unindent();
									}

									bool vOutline = overlayerText2.EnableOutline;
									if (ImGui.Checkbox("开启描边", ref vOutline))
									{
										overlayerText2.EnableOutline = vOutline;
										Main.RequestSave();
									}
									if (vOutline)
									{
										ImGui.Indent();
										if (DrawColorPicker("描边颜色", ref overlayerText2.OutlineColor))
										{
											Main.RequestSave();
										}
										float vOutlineThickness = overlayerText2.OutlineThickness;
										if (ImGui.DragFloat("描边粗细", ref vOutlineThickness, 0.1f, 0f, 8f, "%.1f"))
										{
											overlayerText2.OutlineThickness = vOutlineThickness;
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
									if (ImGui.IsItemDeactivatedAfterEdit())
									{
										ImportResourcePath(ref overlayerText2.FontPath, "Fonts", true);
									}
									ImGui.SameLine();
									if (ImGui.Button("应用字体"))
									{
										ImportResourcePath(ref overlayerText2.FontPath, "Fonts", true);
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
									ImGui.Text("高级动画配置");
									if (overlayerText2.Animations == null) overlayerText2.Animations = new List<OverlayerAnimation>();
									if (overlayerText2.Animations.Count == 0) overlayerText2.Animations.Add(new OverlayerAnimation());
									var mainAnim = overlayerText2.Animations[0];
									
									DrawAnimationPanel(mainAnim, false);
									
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
														string fmt = FormatPreviewTags(array[m]);
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
										if (ImGui.IsItemDeactivatedAfterEdit())
										{
											ImportResourcePath(ref overlayerImage.ImagePath, "Images", false);
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
										
										int imageDepth = RenderDepth.ClampDepth(overlayerImage.Depth);
										ImGui.SetNextItemWidth(180f);
										if (ImGui.SliderInt("\u6DF1\u5EA6##ovImageDepth", ref imageDepth, RenderDepth.MinDepth, RenderDepth.MaxDepth))
										{
											overlayerImage.Depth = RenderDepth.ClampDepth(imageDepth);
											Main.RequestSave();
										}

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
										if (ImGui.SliderFloat("锚点 X (Pivot)##imgPivotX", ref imgPX, 0f, 1f, "%.2f"))
										{
											overlayerImage.PivotX = imgPX;
											Main.RequestSave();
										}
										ImGui.SameLine();
										ImGui.SetNextItemWidth(120f);
										if (ImGui.SliderFloat("锚点 Y (Pivot)##imgPivotY", ref imgPY, 0f, 1f, "%.2f"))
										{
											overlayerImage.PivotY = imgPY;
											Main.RequestSave();
										}

										ImGui.Text("快速对齐:");
										ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(2f, 2f));
										ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(4f, 4f));
										
										Vector2 imgBtnSize = new Vector2(28f, 28f);
										Vector2 displaySize = ImGuiController.ScreenDisplaySize;
										
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

										ImGui.Separator();
										ImGui.Text("高级动画配置");
										if (overlayerImage.Animations == null) overlayerImage.Animations = new List<OverlayerAnimation>();
										if (overlayerImage.Animations.Count == 0) overlayerImage.Animations.Add(new OverlayerAnimation());
										var imgAnim = overlayerImage.Animations[0];
										DrawAnimationPanel(imgAnim, true);
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
					if (ImGui.BeginTabItem("进度条"))
					{
						if (Main.Settings.OverlayerProgressBars == null)
						{
							Main.Settings.OverlayerProgressBars = new List<OverlayerProgressBar>();
						}

						List<OverlayerProgressBar> progressBars = Main.Settings.OverlayerProgressBars;
						if (_selectedOvSidebarBarTab >= progressBars.Count)
						{
							_selectedOvSidebarBarTab = progressBars.Count - 1;
						}
						if (_selectedOvSidebarBarTab < 0 && progressBars.Count > 0)
						{
							_selectedOvSidebarBarTab = 0;
						}

						ImGui.BeginChild("OvBarSidebar", new Vector2(140f, 0f), ImGuiChildFlags.Borders);
						if (ImGui.Button("新建进度条 (+)", new Vector2(-1f, 0f)))
						{
							var progressBar = new OverlayerProgressBar();
							progressBar.Name = $"新进度条 {progressBars.Count + 1}";
							progressBars.Add(progressBar);
							_selectedOvSidebarBarTab = progressBars.Count - 1;
							Main.RequestSave();
						}
						ImGui.Separator();
						for (int b = 0; b < progressBars.Count; b++)
						{
							OverlayerProgressBar sidebarBar = progressBars[b];
							string barName = sidebarBar != null && !string.IsNullOrEmpty(sidebarBar.Name) ? sidebarBar.Name : $"进度条 {b + 1}";
							if (ImGui.Selectable($"{barName}##ov_bartab_{b}", _selectedOvSidebarBarTab == b))
							{
								_selectedOvSidebarBarTab = b;
							}
						}
						ImGui.EndChild();
						ImGui.SameLine();

						ImGui.BeginChild("OvBarContent", new Vector2(0f, 0f), ImGuiChildFlags.Borders);
						if (_selectedOvSidebarBarTab >= 0 && _selectedOvSidebarBarTab < progressBars.Count)
						{
							int selectedOvSidebarBarTab = _selectedOvSidebarBarTab;
							if (progressBars[selectedOvSidebarBarTab] == null)
							{
								progressBars[selectedOvSidebarBarTab] = new OverlayerProgressBar();
							}

							OverlayerProgressBar progressBar = progressBars[selectedOvSidebarBarTab];
							if (progressBar.ValueSource == null) progressBar.ValueSource = new OverlayerProgressValueSource(OverlayerProgressValueKind.Progress);
							if (progressBar.MinSource == null) progressBar.MinSource = new OverlayerProgressValueSource(OverlayerProgressValueKind.Constant, 0.0);
							if (progressBar.MaxSource == null) progressBar.MaxSource = new OverlayerProgressValueSource(OverlayerProgressValueKind.Constant, 100.0);
							if (progressBar.BackgroundColor == null || progressBar.BackgroundColor.Length != 4) progressBar.BackgroundColor = new float[] { 0f, 0f, 0f, 0.45f };
							if (progressBar.FillColor == null || progressBar.FillColor.Length != 4) progressBar.FillColor = new float[] { 0.2f, 0.75f, 1f, 0.95f };
							if (progressBar.BorderColor == null || progressBar.BorderColor.Length != 4) progressBar.BorderColor = new float[] { 1f, 1f, 1f, 0.8f };
							if (progressBar.ShadowColor == null || progressBar.ShadowColor.Length != 4) progressBar.ShadowColor = new float[] { 0f, 0f, 0f, 0.45f };
							if (progressBar.ShadowOffset == null || progressBar.ShadowOffset.Length != 2) progressBar.ShadowOffset = new float[] { 2f, 2f };

							ImGui.PushID($"ov_barblock_{selectedOvSidebarBarTab}");
							try
							{
								ImGui.Text($"进度条配置 ({selectedOvSidebarBarTab + 1})");
								ImGui.SameLine(ImGui.GetWindowWidth() - 80f);
								ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.8f, 0.2f, 0.2f, 1f));
								bool deletePressed = ImGui.Button("删除", new Vector2(60f, 24f));
								ImGui.PopStyleColor();

								if (deletePressed)
								{
									progressBars.RemoveAt(selectedOvSidebarBarTab);
									Main.RequestSave();
									if (_selectedOvSidebarBarTab >= progressBars.Count)
									{
										_selectedOvSidebarBarTab = progressBars.Count - 1;
									}
								}
								else
								{
									string barNameInput = progressBar.Name ?? "";
									if (ImGui.InputText("名称", ref barNameInput, 128u))
									{
										progressBar.Name = barNameInput;
										Main.RequestSave();
									}

									bool enabled = progressBar.IsEnabled;
									if (ImGui.Checkbox("启用此进度条", ref enabled))
									{
										progressBar.IsEnabled = enabled;
										Main.RequestSave();
									}

									ImGui.Separator();
									ImGui.Text("数值映射");
									if (DrawProgressValueSourceEditor("当前值", "bar_value", progressBar.ValueSource)) Main.RequestSave();
									if (DrawProgressValueSourceEditor("0% 值", "bar_min", progressBar.MinSource)) Main.RequestSave();
									if (DrawProgressValueSourceEditor("100% 值", "bar_max", progressBar.MaxSource)) Main.RequestSave();

									int fillDirection = (int)progressBar.FillDirection;
									if (fillDirection < 0 || fillDirection > (int)OverlayerProgressFillDirection.TopToBottom)
									{
										fillDirection = 0;
									}
									ImGui.SetNextItemWidth(180f);
									if (ImGui.Combo("填充方向", ref fillDirection, "从左到右\0从右到左\0从下到上\0从上到下\0"))
									{
										progressBar.FillDirection = (OverlayerProgressFillDirection)fillDirection;
										Main.RequestSave();
									}

									bool reverse = progressBar.Reverse;
									if (ImGui.Checkbox("反向填充", ref reverse))
									{
										progressBar.Reverse = reverse;
										Main.RequestSave();
									}
									ImGui.SameLine();
									bool clampValue = progressBar.ClampValue;
									if (ImGui.Checkbox("限制到 0%-100%", ref clampValue))
									{
										progressBar.ClampValue = clampValue;
										Main.RequestSave();
									}

									ImGui.Separator();
									ImGui.Text("位置与尺寸");
									float barPosX = progressBar.PositionX;
									float barPosY = progressBar.PositionY;
									ImGui.SetNextItemWidth(120f);
									if (ImGui.DragFloat("X 位置##barPosX", ref barPosX, 1f, -2000f, 4000f))
									{
										progressBar.PositionX = barPosX;
										Main.RequestSave();
									}
									ImGui.SameLine();
									ImGui.SetNextItemWidth(120f);
									if (ImGui.DragFloat("Y 位置##barPosY", ref barPosY, 1f, -2000f, 4000f))
									{
										progressBar.PositionY = barPosY;
										Main.RequestSave();
									}

									float barWidth = progressBar.Width;
									float barHeight = progressBar.Height;
									ImGui.SetNextItemWidth(120f);
									if (ImGui.DragFloat("宽度##barWidth", ref barWidth, 1f, 1f, 4000f, "%.1f"))
									{
										progressBar.Width = Math.Max(1f, barWidth);
										Main.RequestSave();
									}
									ImGui.SameLine();
									ImGui.SetNextItemWidth(120f);
									if (ImGui.DragFloat("高度##barHeight", ref barHeight, 1f, 1f, 4000f, "%.1f"))
									{
										progressBar.Height = Math.Max(1f, barHeight);
										Main.RequestSave();
									}

									float barOpacity = progressBar.Opacity;
									ImGui.SetNextItemWidth(180f);
									if (ImGui.SliderFloat("不透明度##barOpacity", ref barOpacity, 0f, 1f, "%.2f"))
									{
										progressBar.Opacity = Math.Max(0f, Math.Min(1f, barOpacity));
										Main.RequestSave();
									}

									int barDepth = RenderDepth.ClampDepth(progressBar.Depth);
									ImGui.SetNextItemWidth(180f);
									if (ImGui.SliderInt("\u6DF1\u5EA6##ovProgressDepth", ref barDepth, RenderDepth.MinDepth, RenderDepth.MaxDepth))
									{
										progressBar.Depth = RenderDepth.ClampDepth(barDepth);
										Main.RequestSave();
									}

									float barPX = progressBar.PivotX;
									float barPY = progressBar.PivotY;
									ImGui.SetNextItemWidth(120f);
									if (ImGui.SliderFloat("锚点 X (Pivot)##barPivotX", ref barPX, 0f, 1f, "%.2f"))
									{
										progressBar.PivotX = barPX;
										Main.RequestSave();
									}
									ImGui.SameLine();
									ImGui.SetNextItemWidth(120f);
									if (ImGui.SliderFloat("锚点 Y (Pivot)##barPivotY", ref barPY, 0f, 1f, "%.2f"))
									{
										progressBar.PivotY = barPY;
										Main.RequestSave();
									}

									ImGui.Text("快速对齐:");
									ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(2f, 2f));
									ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(4f, 4f));
									Vector2 barBtnSize = new Vector2(28f, 28f);
									Vector2 displaySize = ImGuiController.ScreenDisplaySize;
									if (ImGui.Button("↖##bar_tl", barBtnSize)) AlignOvProgressBar(progressBar, 0, displaySize); ImGui.SameLine();
									if (ImGui.Button("↑##bar_tc", barBtnSize)) AlignOvProgressBar(progressBar, 1, displaySize); ImGui.SameLine();
									if (ImGui.Button("↗##bar_tr", barBtnSize)) AlignOvProgressBar(progressBar, 2, displaySize);
									if (ImGui.Button("←##bar_ml", barBtnSize)) AlignOvProgressBar(progressBar, 3, displaySize); ImGui.SameLine();
									if (ImGui.Button("┼##bar_cc", barBtnSize)) AlignOvProgressBar(progressBar, 4, displaySize); ImGui.SameLine();
									if (ImGui.Button("→##bar_mr", barBtnSize)) AlignOvProgressBar(progressBar, 5, displaySize);
									if (ImGui.Button("↙##bar_bl", barBtnSize)) AlignOvProgressBar(progressBar, 6, displaySize); ImGui.SameLine();
									if (ImGui.Button("↓##bar_bc", barBtnSize)) AlignOvProgressBar(progressBar, 7, displaySize); ImGui.SameLine();
									if (ImGui.Button("↘##bar_br", barBtnSize)) AlignOvProgressBar(progressBar, 8, displaySize);
									ImGui.PopStyleVar(2);

									ImGui.Separator();
									ImGui.Text("样式");
									if (DrawColorPicker("背景颜色", ref progressBar.BackgroundColor)) Main.RequestSave();
									if (DrawColorPicker("填充颜色", ref progressBar.FillColor)) Main.RequestSave();
									if (DrawColorPicker("边框颜色", ref progressBar.BorderColor)) Main.RequestSave();

									float cornerRadius = progressBar.CornerRadius;
									ImGui.SetNextItemWidth(160f);
									if (ImGui.DragFloat("圆角半径##barCornerRadius", ref cornerRadius, 0.5f, 0f, 256f, "%.1f"))
									{
										progressBar.CornerRadius = Math.Max(0f, cornerRadius);
										Main.RequestSave();
									}

									float borderThickness = progressBar.BorderThickness;
									ImGui.SetNextItemWidth(160f);
									if (ImGui.DragFloat("边框粗细##barBorderThickness", ref borderThickness, 0.1f, 0f, 20f, "%.1f"))
									{
										progressBar.BorderThickness = Math.Max(0f, borderThickness);
										Main.RequestSave();
									}

									ImGui.Separator();
									bool shadowEnabled = progressBar.EnableShadow;
									if (ImGui.Checkbox("开启阴影##barShadowEnabled", ref shadowEnabled))
									{
										progressBar.EnableShadow = shadowEnabled;
										Main.RequestSave();
									}
									if (shadowEnabled)
									{
										if (DrawColorPicker("阴影颜色##barShadowColor", ref progressBar.ShadowColor)) Main.RequestSave();

										Vector2 shadowOffset = new Vector2(progressBar.ShadowOffset[0], progressBar.ShadowOffset[1]);
										ImGui.SetNextItemWidth(180f);
										if (ImGui.DragFloat2("阴影偏移##barShadowOffset", ref shadowOffset, 0.1f))
										{
											progressBar.ShadowOffset[0] = shadowOffset.X;
											progressBar.ShadowOffset[1] = shadowOffset.Y;
											Main.RequestSave();
										}

										float shadowSoftness = progressBar.ShadowSoftness;
										ImGui.SetNextItemWidth(160f);
										if (ImGui.DragFloat("阴影柔度##barShadowSoftness", ref shadowSoftness, 0.5f, 0f, 64f, "%.1f"))
										{
											progressBar.ShadowSoftness = Math.Max(0f, shadowSoftness);
											Main.RequestSave();
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
							ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1f), "请在左侧新建或选择一个进度条");
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
		ImGui.SetNextWindowSize(new Vector2(480f, 360f), ImGuiCond.FirstUseEver);
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
			if (!_editingImGuiPanelScale)
			{
				_pendingImGuiPanelScale = Main.Settings.ImGuiPanelScale;
			}
			float imguiPanelScale = _pendingImGuiPanelScale;
			if (ImGui.SliderFloat("ImGui 面板缩放", ref imguiPanelScale, 0.6f, 2.0f, "%.2f"))
			{
				_pendingImGuiPanelScale = Math.Max(0.6f, Math.Min(2.0f, imguiPanelScale));
				_editingImGuiPanelScale = true;
			}
			if (ImGui.IsItemDeactivatedAfterEdit())
			{
				Main.Settings.ImGuiPanelScale = Math.Max(0.6f, Math.Min(2.0f, _pendingImGuiPanelScale));
				_editingImGuiPanelScale = false;
				Main.RequestSave();
			}
			if (!_editingOverlayUpdateRate)
			{
				_pendingOverlayUpdateRate = Main.Settings.OverlayUpdateRate;
			}
			float overlayUpdateRate = _pendingOverlayUpdateRate;
			if (ImGui.SliderFloat("覆盖层刷新率", ref overlayUpdateRate, 30.0f, 360.0f, "%.0f FPS"))
			{
				_pendingOverlayUpdateRate = Math.Max(30.0f, Math.Min(360.0f, overlayUpdateRate));
				_editingOverlayUpdateRate = true;
			}
			if (ImGui.IsItemDeactivatedAfterEdit())
			{
				Main.Settings.OverlayUpdateRate = Math.Max(30.0f, Math.Min(360.0f, _pendingOverlayUpdateRate));
				_editingOverlayUpdateRate = false;
				Main.RequestSave();
			}
			if (!_editingImageRenderScale)
			{
				_pendingImageRenderScale = Main.Settings.ImageRenderScale;
			}
			float imageRenderScale = _pendingImageRenderScale;
			if (ImGui.SliderFloat("图片渲染倍率", ref imageRenderScale, 0.25f, 2.0f, "%.2f"))
			{
				_pendingImageRenderScale = Math.Max(0.25f, Math.Min(2.0f, imageRenderScale));
				_editingImageRenderScale = true;
			}
			if (ImGui.IsItemDeactivatedAfterEdit())
			{
				Main.Settings.ImageRenderScale = Math.Max(0.25f, Math.Min(2.0f, _pendingImageRenderScale));
				_editingImageRenderScale = false;
				TextureManager.ClearScaledTextures();
				Main.RequestSave();
			}

			ImGui.Separator();
			ImGui.Text("\u5F00\u53D1\u8005\u9009\u9879");
			if (Main.Settings.GameUIDeveloperUnlocked)
			{
				ImGui.TextColored(new System.Numerics.Vector4(0.3f, 1f, 0.45f, 1f), "Game UI \u9AD8\u7EA7\u63A7\u5236\u5DF2\u89E3\u9501");
				ImGui.SameLine();
				if (ImGui.Button("\u91CD\u65B0\u9501\u5B9A##GameUIDevLock"))
				{
					Main.Settings.GameUIDeveloperUnlocked = false;
					if (GameUIManager.Instance != null)
					{
						GameUIManager.Instance.RestoreAll();
					}
					Main.RequestSave();
				}
			}
			else
			{
				ImGui.SetNextItemWidth(160f);
				if (ImGui.InputText("\u8F93\u5165 Key##GameUIDevKey", ref _gameUIDeveloperKeyInput, 32u))
				{
					_gameUIDeveloperKeyFailed = false;
				}
				ImGui.SameLine();
				if (ImGui.Button("\u9A8C\u8BC1##GameUIDevVerify"))
				{
					string key = (_gameUIDeveloperKeyInput ?? string.Empty).Trim();
					if (string.Equals(key, GameUIManager.DeveloperUnlockKey, StringComparison.OrdinalIgnoreCase))
					{
						Main.Settings.GameUIDeveloperUnlocked = true;
						_gameUIDeveloperKeyInput = "";
						_gameUIDeveloperKeyFailed = false;
						Main.RequestSave();
					}
					else
					{
						_gameUIDeveloperKeyFailed = true;
					}
				}
				ImGui.TextColored(new System.Numerics.Vector4(0.75f, 0.75f, 0.75f, 1f), "\u7528\u4E8E\u89E3\u9501\u5224\u5B9A\u6A21\u5F0F/\u4E0D\u4F1A\u5931\u8D25/\u81EA\u52A8\u6F14\u594F\u7684\u4F4D\u7F6E\u3001\u7F29\u653E\u548C\u900F\u660E\u5EA6\u63A7\u5236\u3002");
				if (_gameUIDeveloperKeyFailed)
				{
					ImGui.TextColored(new System.Numerics.Vector4(1f, 0.35f, 0.35f, 1f), "Key \u9A8C\u8BC1\u5931\u8D25");
				}
			}
			ImGui.Separator();
			ImGui.Spacing();
			string text3 = Path.Combine(Application.dataPath, "../CheryTools_Settings_Backup.xml");
			string cytPath = Path.Combine(Application.dataPath, "../CheryTools_Settings_Backup.cyt");
			if (ImGui.Button("导出配置 (.cyt)"))
			{
				try
				{
					Main.Settings.Save(Main.ModEntry);
					cytPath = CheryToolsAssets.ExportCytPackage(Main.Settings);
					Main.Logger.Log("Settings exported to: " + cytPath);
				}
				catch (Exception ex)
				{
					Main.Logger.Log("Failed to export settings: " + ex.ToString());
				}
			}
			ImGui.SameLine();
			if (ImGui.Button("导入配置 (.cyt)"))
			{
				try
				{
					if (File.Exists(cytPath))
					{
						string destFileName = Path.Combine(Main.ModEntry.Path, "Settings.xml");
						CheryToolsAssets.ImportCytPackage(cytPath, destFileName);
						ReloadSettingsAfterImport(cytPath);
					}
				}
				catch (Exception ex2)
				{
					Main.Logger.Log("Failed to import settings: " + ex2.ToString());
				}
			}
			if (ImGui.Button("导入配置 (XML)"))
			{
				try
				{
					if (File.Exists(text3))
					{
						string destFileName = Path.Combine(Main.ModEntry.Path, "Settings.xml");
						File.Copy(text3, destFileName, overwrite: true);
						ReloadSettingsAfterImport(text3);
					}
				}
				catch (Exception ex2)
				{
					Main.Logger.Log("Failed to import settings: " + ex2.ToString());
				}
			}
			if (File.Exists(text3) || File.Exists(cytPath))
			{
				ImGui.SameLine();
				ImGui.TextColored(new System.Numerics.Vector4(0.5f, 0.5f, 0.5f, 1f), "配置已保存在游戏根目录!");
			}
			ImGui.Separator();
			ImGui.Text("旧 KV 配置迁移");
			ImGui.TextColored(new System.Numerics.Vector4(0.65f, 0.65f, 0.65f, 1f), "追加导入旧 16K/12K/10K/8K 为新 KV 配置，不覆盖当前配置。");
			string currentSettingsPath = Path.Combine(Main.ModEntry.Path, "Settings.xml");
			if (ImGui.Button("从当前 Settings.xml 导入旧 KV"))
			{
				ImportLegacyKeyViewerFromXml(currentSettingsPath);
			}
			ImGui.SameLine();
			if (ImGui.Button("从备份 XML 导入旧 KV"))
			{
				ImportLegacyKeyViewerFromXml(text3);
			}
			if (ImGui.Button("从备份 .cyt 导入旧 KV"))
			{
				ImportLegacyKeyViewerFromCyt(cytPath);
			}
			if (!string.IsNullOrEmpty(_legacyKeyViewerImportMessage))
			{
				ImGui.TextColored(new System.Numerics.Vector4(0.75f, 0.85f, 1f, 1f), _legacyKeyViewerImportMessage);
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
				float r = Convert.ToInt32(hex.Substring(0, 2), 16) / 255f;
				float g = Convert.ToInt32(hex.Substring(2, 2), 16) / 255f;
				float b = Convert.ToInt32(hex.Substring(4, 2), 16) / 255f;
				float a = hex.Length == 8 ? Convert.ToInt32(hex.Substring(6, 2), 16) / 255f : 1f;
				return new System.Numerics.Vector4(r, g, b, a);
			}
			catch
			{
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

	private static void AlignOvProgressBar(OverlayerProgressBar bar, int type, Vector2 displaySize)
	{
		if (bar == null) return;

		switch (type)
		{
			case 0:
				bar.PositionX = 20f;
				bar.PositionY = 20f;
				bar.PivotX = 0f;
				bar.PivotY = 0f;
				break;
			case 1:
				bar.PositionX = displaySize.X * 0.5f;
				bar.PositionY = 20f;
				bar.PivotX = 0.5f;
				bar.PivotY = 0f;
				break;
			case 2:
				bar.PositionX = displaySize.X - 20f;
				bar.PositionY = 20f;
				bar.PivotX = 1f;
				bar.PivotY = 0f;
				break;
			case 3:
				bar.PositionX = 20f;
				bar.PositionY = displaySize.Y * 0.5f;
				bar.PivotX = 0f;
				bar.PivotY = 0.5f;
				break;
			case 4:
				bar.PositionX = displaySize.X * 0.5f;
				bar.PositionY = displaySize.Y * 0.5f;
				bar.PivotX = 0.5f;
				bar.PivotY = 0.5f;
				break;
			case 5:
				bar.PositionX = displaySize.X - 20f;
				bar.PositionY = displaySize.Y * 0.5f;
				bar.PivotX = 1f;
				bar.PivotY = 0.5f;
				break;
			case 6:
				bar.PositionX = 20f;
				bar.PositionY = displaySize.Y - 20f;
				bar.PivotX = 0f;
				bar.PivotY = 1f;
				break;
			case 7:
				bar.PositionX = displaySize.X * 0.5f;
				bar.PositionY = displaySize.Y - 20f;
				bar.PivotX = 0.5f;
				bar.PivotY = 1f;
				break;
			case 8:
				bar.PositionX = displaySize.X - 20f;
				bar.PositionY = displaySize.Y - 20f;
				bar.PivotX = 1f;
				bar.PivotY = 1f;
				break;
		}
		Main.RequestSave();
	}

	private static void DrawEasingCell(string name, string displayName, ref string selectedEasing, float width, float height)
	{
		var drawList = ImGui.GetWindowDrawList();
		var pos = ImGui.GetCursorScreenPos();
		
		bool isSelected = (selectedEasing.ToLowerInvariant() == name.ToLowerInvariant().Replace("-", "").Replace(" ", ""));
		uint bgCol = ImGui.GetColorU32(ImGuiCol.FrameBg);
		uint borderCol = isSelected ? 0xFF00FFFF : 0x44FFFFFF;
		
		ImGui.PushID("ease_" + name);
		bool clicked = ImGui.InvisibleButton("##btn", new System.Numerics.Vector2(width, height + 20f));
		bool hovered = ImGui.IsItemHovered();
		ImGui.PopID();
		
		if (hovered && !isSelected)
		{
			borderCol = 0x88FFFFFF;
		}
		
		drawList.AddRectFilled(pos, pos + new System.Numerics.Vector2(width, height), bgCol, 4f);
		drawList.AddRect(pos, pos + new System.Numerics.Vector2(width, height), borderCol, 4f, ImDrawFlags.None, isSelected ? 2f : 1f);
		
		int segments = 30;
		System.Numerics.Vector2 prevPt = new System.Numerics.Vector2(pos.X, pos.Y + height - EasingUtil.EvaluateEasing(0f, name) * height);
		
		uint lineCol = isSelected ? 0xFF00FFFF : 0xFFFFFFFF;
		for (int s = 1; s <= segments; s++)
		{
			float t = (float)s / segments;
			float val = EasingUtil.EvaluateEasing(t, name);
			System.Numerics.Vector2 currPt = new System.Numerics.Vector2(
				pos.X + t * width,
				pos.Y + height - val * height
			);
			drawList.AddLine(prevPt, currPt, lineCol, 1.5f);
			prevPt = currPt;
		}
		
		System.Numerics.Vector2 textPos = new System.Numerics.Vector2(pos.X + 4f, pos.Y + height + 2f);
		drawList.AddText(textPos, isSelected ? 0xFF00FFFF : 0xFFD8D8D8, displayName);
		
		if (clicked)
		{
			selectedEasing = name;
			ImGui.CloseCurrentPopup();
		}
	}

	private static void DrawAnimationPanel(OverlayerAnimation anim, bool isImage)
	{
		if (ImGui.Checkbox("启用动画##chk_en", ref anim.IsEnabled))
		{
			Main.RequestSave();
		}
		ImGui.SameLine();
		
		int triggerIndex = (int)anim.Trigger;
		ImGui.SetNextItemWidth(150f);
		if (ImGui.Combo("触发条件##combo_trig", ref triggerIndex, "当点击时\0当Combo增加时\0"))
		{
			anim.Trigger = (AnimationTrigger)triggerIndex;
			Main.RequestSave();
		}
		
		ImGui.SameLine();
		if (ImGui.Checkbox("使用图形化编辑##chk_graph", ref anim.UseGraphicalAnimation))
		{
			anim.ParseJson();
			Main.RequestSave();
		}
		
		if (anim.UseGraphicalAnimation)
		{
			ImGui.Spacing();
			float startX = ImGui.GetCursorPosX();
			float colWidth = 110f;
			float itemWidth = 100f;
			
			// --- Row 1 Headers (Start) ---
			ImGui.Text("起始大小"); 
			ImGui.SameLine(startX + 1f * colWidth);
			if (isImage) 
			{ 
				ImGui.Text("起始旋转"); 
				ImGui.SameLine(startX + 2f * colWidth); 
			}
			ImGui.Text("起始X位置"); 
			ImGui.SameLine(startX + (isImage ? 3f : 2f) * colWidth);
			ImGui.Text("起始Y位置"); 
			if (isImage) 
			{ 
				ImGui.SameLine(startX + 4f * colWidth); 
				ImGui.Text("起始透明度"); 
			}
			
			// --- Row 1 Inputs ---
			ImGui.SetNextItemWidth(itemWidth);
			if (ImGui.DragFloat("##start_scale_" + anim.GetHashCode(), ref anim.StartScale, 0.05f, 0f, 10f, "%.2f")) { anim.ParseJson(); Main.RequestSave(); }
			
			ImGui.SameLine(startX + 1f * colWidth);
			if (isImage)
			{
				ImGui.SetNextItemWidth(itemWidth);
				if (ImGui.DragFloat("##start_rot_" + anim.GetHashCode(), ref anim.StartRotation, 1f, -360f, 360f, "%.1f")) { anim.ParseJson(); Main.RequestSave(); }
				ImGui.SameLine(startX + 2f * colWidth);
			}
			
			ImGui.SetNextItemWidth(itemWidth);
			if (ImGui.DragFloat("##start_x_" + anim.GetHashCode(), ref anim.StartX, 1f, -2000f, 2000f, "%.1f")) { anim.ParseJson(); Main.RequestSave(); }
			
			ImGui.SameLine(startX + (isImage ? 3f : 2f) * colWidth);
			ImGui.SetNextItemWidth(itemWidth);
			if (ImGui.DragFloat("##start_y_" + anim.GetHashCode(), ref anim.StartY, 1f, -2000f, 2000f, "%.1f")) { anim.ParseJson(); Main.RequestSave(); }
			
			if (isImage)
			{
				ImGui.SameLine(startX + 4f * colWidth);
				ImGui.SetNextItemWidth(itemWidth);
				if (ImGui.SliderFloat("##start_op_" + anim.GetHashCode(), ref anim.StartOpacity, 0f, 1f, "%.2f")) { anim.ParseJson(); Main.RequestSave(); }
			}
			
			ImGui.Spacing();
			ImGui.Spacing();
			
			// --- Row 2 Headers (End) ---
			ImGui.Text("最终大小"); 
			ImGui.SameLine(startX + 1f * colWidth);
			if (isImage) 
			{ 
				ImGui.Text("最终旋转"); 
				ImGui.SameLine(startX + 2f * colWidth); 
			}
			ImGui.Text("最终X位置"); 
			ImGui.SameLine(startX + (isImage ? 3f : 2f) * colWidth);
			ImGui.Text("最终Y位置"); 
			if (isImage) 
			{ 
				ImGui.SameLine(startX + 4f * colWidth); 
				ImGui.Text("最终透明度"); 
			}
			
			// --- Row 2 Inputs ---
			ImGui.SetNextItemWidth(itemWidth);
			if (ImGui.DragFloat("##end_scale_" + anim.GetHashCode(), ref anim.EndScale, 0.05f, 0f, 10f, "%.2f")) { anim.ParseJson(); Main.RequestSave(); }
			
			ImGui.SameLine(startX + 1f * colWidth);
			if (isImage)
			{
				ImGui.SetNextItemWidth(itemWidth);
				if (ImGui.DragFloat("##end_rot_" + anim.GetHashCode(), ref anim.EndRotation, 1f, -360f, 360f, "%.1f")) { anim.ParseJson(); Main.RequestSave(); }
				ImGui.SameLine(startX + 2f * colWidth);
			}
			
			ImGui.SetNextItemWidth(itemWidth);
			if (ImGui.DragFloat("##end_x_" + anim.GetHashCode(), ref anim.EndX, 1f, -2000f, 2000f, "%.1f")) { anim.ParseJson(); Main.RequestSave(); }
			
			ImGui.SameLine(startX + (isImage ? 3f : 2f) * colWidth);
			ImGui.SetNextItemWidth(itemWidth);
			if (ImGui.DragFloat("##end_y_" + anim.GetHashCode(), ref anim.EndY, 1f, -2000f, 2000f, "%.1f")) { anim.ParseJson(); Main.RequestSave(); }
			
			if (isImage)
			{
				ImGui.SameLine(startX + 4f * colWidth);
				ImGui.SetNextItemWidth(itemWidth);
				if (ImGui.SliderFloat("##end_op_" + anim.GetHashCode(), ref anim.EndOpacity, 0f, 1f, "%.2f")) { anim.ParseJson(); Main.RequestSave(); }
			}
			
			ImGui.Spacing();
			
			float dur = anim.Duration;
			ImGui.SetNextItemWidth(120f);
			if (ImGui.DragFloat("持续时间 (秒)##dur_" + anim.GetHashCode(), ref dur, 0.05f, 0.01f, 10f, "%.2f s"))
			{
				anim.Duration = dur;
				anim.ParseJson();
				Main.RequestSave();
			}
			
			ImGui.SameLine();
			ImGui.AlignTextToFramePadding();
			ImGui.Text($"当前缓动: {anim.EasingType}");
			ImGui.SameLine();
			if (ImGui.Button("预览选择缓动类型##btn_ease_" + anim.GetHashCode()))
			{
				ImGui.OpenPopup("EasingSelectorPopup_" + anim.GetHashCode());
			}
			
			string oldEasing = anim.EasingType;
			ImGui.SetNextWindowSize(new System.Numerics.Vector2(710f, 520f), ImGuiCond.Appearing);
			if (ImGui.BeginPopup("EasingSelectorPopup_" + anim.GetHashCode()))
			{
				ImGui.Text("选择缓动类型");
				ImGui.Separator();
				
				string[] easingNames = new string[]
				{
					"linear",
					"ease-in-sine", "ease-out-sine", "ease-in-out-sine",
					"ease-in-quad", "ease-out-quad", "ease-in-out-quad",
					"ease-in-cubic", "ease-out-cubic", "ease-in-out-cubic",
					"ease-in-quart", "ease-out-quart", "ease-in-out-quart",
					"ease-in-quint", "ease-out-quint", "ease-in-out-quint",
					"ease-in-expo", "ease-out-expo", "ease-in-out-expo",
					"ease-in-circ", "ease-out-circ", "ease-in-out-circ",
					"ease-in-back", "ease-out-back", "ease-in-out-back"
				};
				
				float cellW = 120f;
				float cellH = 65f;
				float spacingX = 18f;
				float spacingY = 15f;
				int cols = 5;
				
				for (int i = 0; i < easingNames.Length; i++)
				{
					string easeName = easingNames[i];
					DrawEasingCell(easeName, easeName, ref anim.EasingType, cellW, cellH);
					
					if ((i + 1) % cols != 0)
					{
						ImGui.SameLine(0f, spacingX);
					}
					else
					{
						ImGui.Dummy(new System.Numerics.Vector2(0f, spacingY));
					}
				}
				ImGui.EndPopup();
			}
			
			if (anim.EasingType != oldEasing)
			{
				anim.ParseJson();
				Main.RequestSave();
			}
		}
		else
		{
			if (ImGui.InputTextMultiline("##JsonEditor_" + anim.GetHashCode(), ref anim.JsonString, 8192, new System.Numerics.Vector2(-1, 150)))
			{
				Main.RequestSave();
			}
			
			if (ImGui.Button("应用/解析 JSON##btn_parse_" + anim.GetHashCode(), new System.Numerics.Vector2(150, 30)))
			{
				anim.ParseJson();
				Main.RequestSave();
			}
		}
		
		ImGui.SameLine();
		if (ImGui.Button("播放动画##btn_play_" + anim.GetHashCode()))
		{
			anim.ParseJson();
			if (OverlayerManager.Instance != null)
			{
				var state = OverlayerManager.Instance.GetAnimState(anim);
				state.IsPlaying = true;
				state.CurrentTime = 0f;
			}
		}
	}
}
