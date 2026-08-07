using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
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
	private static string _tagSearchText = "";

	private struct TagInsertItem
	{
		public string Tag;
		public string Desc;
		public bool PlaceCursorInside;

		public TagInsertItem(string tag, string desc, bool placeCursorInside = false)
		{
			Tag = tag;
			Desc = desc;
			PlaceCursorInside = placeCursorInside;
		}
	}

	private static readonly TagInsertItem[] AllTagInsertItems = new TagInsertItem[]
	{
		new TagInsertItem("{fps}", "当前 FPS"),
		new TagInsertItem("{fps:1}", "当前 FPS，最多 1 位小数"),
		new TagInsertItem("{combo}", "当前 Pure Combo"),
		new TagInsertItem("{combo:p}", "当前 Perfect Combo"),
		new TagInsertItem("{score}", "当前计分，满分 1000000"),
		new TagInsertItem("{music}", "当前曲目完整信息"),
		new TagInsertItem("{artist}", "当前曲师"),
		new TagInsertItem("{title}", "当前曲名"),
		new TagInsertItem("{ttile}", "总轨道数量"),
		new TagInsertItem("{atile}", "经过的轨道数量"),
		new TagInsertItem("{level}", "关卡制作者"),
		new TagInsertItem("{x}", "关卡倍速"),
		new TagInsertItem("{xperfect:xpp}", "XPerfect / XPurePerfect 数量"),
		new TagInsertItem("{xperfect:epp}", "XPerfect / Early PurePerfect 数量"),
		new TagInsertItem("{xperfect:lpp}", "XPerfect / Late PurePerfect 数量"),
		new TagInsertItem("{Attempts}", "当前谱面尝试次数"),
		new TagInsertItem("{Checkpointused}", "本次游玩使用过的检查点数"),
		new TagInsertItem("{Curcheckpoint}", "本次游玩经过的检查点数"),
		new TagInsertItem("{Totalcheckpoint}", "谱面检查点总数"),
		new TagInsertItem("{GameVersion}", "游戏版本"),
		new TagInsertItem("{CheryToolsVersion}", "CheryTools 版本"),
		new TagInsertItem("{TotalPlaytime}", "当前谱面累计游玩时间"),
		new TagInsertItem("{MinFPS}", "本次游玩最低帧率"),
		new TagInsertItem("{MaxFPS}", "本次游玩最高帧率"),
		new TagInsertItem("{bpm}", "基础 BPM，最多 2 位小数"),
		new TagInsertItem("{bpm:2}", "基础 BPM，最多 2 位小数"),
		new TagInsertItem("{tbpm}", "含轨道速度乘数的 BPM，最多 2 位小数"),
		new TagInsertItem("{tbpm:2}", "含轨道速度乘数的 BPM，最多 2 位小数"),
		new TagInsertItem("{cbpm}", "基于地板时间的当前真实 BPM，最多 2 位小数"),
		new TagInsertItem("{cbpm:2}", "基于地板时间的当前真实 BPM，最多 2 位小数"),
		new TagInsertItem("{cur}", "当前真实 BPM 下的每秒点击次数"),
		new TagInsertItem("{maptime}", "地图总时间"),
		new TagInsertItem("{maptime:p}", "地图已游玩时间"),
		new TagInsertItem("{musictime}", "音乐总时间"),
		new TagInsertItem("{musictime:p}", "音乐已播放时间"),
		new TagInsertItem("{datey}", "当前年份"),
		new TagInsertItem("{datem}", "当前月份"),
		new TagInsertItem("{dated}", "当前日期"),
		new TagInsertItem("{wtime}", "电脑时间，24 小时制"),
		new TagInsertItem("{wtime12}", "电脑时间，12 小时制"),
		new TagInsertItem("{judge}", "当前判定模式"),
		new TagInsertItem("{interval}", "定时窗口大小百分比"),
		new TagInsertItem("{timing}", "本次击打延迟 ms，可为负数"),
		new TagInsertItem("{timing:2}", "本次击打延迟 ms，最多 2 位小数"),
		new TagInsertItem("{acc}", "准确率，最多 2 位小数"),
		new TagInsertItem("{acc:2}", "准确率，最多 2 位小数"),
		new TagInsertItem("{xacc}", "X-Accuracy，最多 2 位小数"),
		new TagInsertItem("{xacc:2}", "X-Accuracy，最多 2 位小数"),
		new TagInsertItem("{progress}", "地图进度，最多 2 位小数"),
		new TagInsertItem("{progress:2}", "地图进度，最多 2 位小数"),
		new TagInsertItem("{te}", "Too Early 数量"),
		new TagInsertItem("{ve}", "Very Early 数量"),
		new TagInsertItem("{ep}", "Early Perfect 数量"),
		new TagInsertItem("{p}", "Pure Perfect 数量"),
		new TagInsertItem("{lp}", "Late Perfect 数量"),
		new TagInsertItem("{vl}", "Very Late 数量"),
		new TagInsertItem("{tl}", "Too Late 数量"),
		new TagInsertItem("{fm}", "错过数量"),
		new TagInsertItem("{fo}", "按太快数量"),
		new TagInsertItem("{miss}", "错过 + 按太快合计"),
		new TagInsertItem("<color=#D4D4D6FF></color>", "带透明度的文字颜色", true),
		new TagInsertItem("<color=#FFFFFFFF></color>", "白色，带透明度", true),
		new TagInsertItem("<size=150%></size>", "相对字号", true),
		new TagInsertItem("<size=32></size>", "绝对字号", true)
	};

	private static readonly KeyCode[] SettingsHotkeyCandidates = BuildSettingsHotkeyCandidates();

	private static KeyCode[] BuildSettingsHotkeyCandidates()
	{
		Array values = Enum.GetValues(typeof(KeyCode));
		var keys = new List<KeyCode>();
		for (int i = 0; i < values.Length; i++)
		{
			KeyCode key = (KeyCode)values.GetValue(i);
			if ((int)key != 323)
			{
				keys.Add(key);
			}
		}
		return keys.ToArray();
	}

	private static void DrawLiteralTagText(string text)
	{
		ImGui.TextUnformatted(text ?? string.Empty);
	}

	private static void DrawHelpBullet(string key, string fallback)
	{
		ImGui.Bullet();
		ImGui.SameLine();
		ImGui.PushTextWrapPos(0f);
		ImGui.TextUnformatted(Tr(key, fallback));
		ImGui.PopTextWrapPos();
	}

	private static void DrawSponsorEntry(string name, string url, int id)
	{
		ImGui.Bullet();
		ImGui.SameLine();
		ImGui.PushFont(ImGuiController.ChineseDefaultUIFont);
		try
		{
			ImGui.TextUnformatted(name ?? string.Empty);
		}
		finally
		{
			ImGui.PopFont();
		}
		if (!string.IsNullOrWhiteSpace(url))
		{
			ImGui.SameLine(300f);
			if (ImGui.SmallButton(Tr("help.sponsors.openBilibili", "打开 Bilibili") + "##SponsorLink_" + id.ToString(CultureInfo.InvariantCulture)))
			{
				Application.OpenURL(url);
			}
		}
	}

	private static bool DrawTopBarButton(string label, bool active)
	{
		Vector4 normalBg = new Vector4(0f, 0f, 0f, 0f);
		Vector4 hoverBg = new Vector4(1f, 1f, 1f, 0.10f);
		Vector4 activeBg = new Vector4(0.10f, 0.42f, 0.95f, 0.86f);
		Vector4 activeHoverBg = new Vector4(0.14f, 0.50f, 1f, 0.94f);
		Vector4 pressedBg = new Vector4(0.07f, 0.32f, 0.78f, 1f);
		Vector4 textColor = new Vector4(0.95f, 0.97f, 0.99f, 1f);

		ImGui.PushStyleColor(ImGuiCol.Button, active ? activeBg : normalBg);
		ImGui.PushStyleColor(ImGuiCol.ButtonHovered, active ? activeHoverBg : hoverBg);
		ImGui.PushStyleColor(ImGuiCol.ButtonActive, pressedBg);
		ImGui.PushStyleColor(ImGuiCol.Text, textColor);
		ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(10f, 3f));
		ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 4f);
		try
		{
			return ImGui.Button(label);
		}
		finally
		{
			ImGui.PopStyleVar(2);
			ImGui.PopStyleColor(4);
		}
	}

	private static string Tr(string key, string fallback)
	{
		return LocalizationManager.T(key, fallback);
	}

	private static string SingleLinePreview(string text, int maxChars)
	{
		if (string.IsNullOrEmpty(text))
		{
			return "";
		}

		string normalized = text.Replace("\r", "").Replace("\n", "\\n");
		if (maxChars > 3 && normalized.Length > maxChars)
		{
			return normalized.Substring(0, maxChars - 3) + "...";
		}
		return normalized;
	}

	private static string TrTagDesc(TagInsertItem item)
	{
		return LocalizationManager.T("tag." + item.Tag, item.Desc);
	}

	private static string TrGameUITarget(GameUITargetDefinition target)
	{
		if (target == null)
		{
			return string.Empty;
		}

		return LocalizationManager.T("tools.gameUi.target." + target.Id, target.DisplayName);
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

		RichTextCodeEditor.SetCursor($"TextFormatEditorWindow_{_overlayerTextEditorIndex}", ovText.TextFormat, _lastTextFormatCursorPos);
		Main.RequestSave();
	}

	private void DrawOverlayerTagInsertPopup(OverlayerText ovText)
	{
		Vector2 popupSize = new Vector2(640f, 430f);
		ImGui.SetNextWindowSize(popupSize, ImGuiCond.Always);
		ImGui.SetNextWindowSizeConstraints(popupSize, popupSize);
		if (!ImGui.BeginPopup("TagSelectorPopup"))
		{
			return;
		}

		ImGui.Text(Tr("tag.searchLabel", "搜索并插入 Tag:"));
		ImGui.SetNextItemWidth(-1f);
		ImGui.InputText("##TagSearchInput", ref _tagSearchText, 64u);
		ImGui.Separator();

		string query = (_tagSearchText ?? string.Empty).Trim();
		if (ImGui.BeginTable("TagSearchTable", 3, ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY, new Vector2(0f, 340f)))
		{
			ImGui.TableSetupColumn("标签", ImGuiTableColumnFlags.WidthFixed, 180f);
			ImGui.TableSetupColumn("说明", ImGuiTableColumnFlags.WidthStretch);
			ImGui.TableSetupColumn("操作", ImGuiTableColumnFlags.WidthFixed, 72f);
			ImGui.TableHeadersRow();

			foreach (TagInsertItem item in AllTagInsertItems)
			{
				string desc = TrTagDesc(item);
				if (!string.IsNullOrEmpty(query)
					&& (item.Tag == null || item.Tag.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0)
					&& (desc == null || desc.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0))
				{
					continue;
				}

				ImGui.TableNextRow();
				ImGui.TableNextColumn();
				DrawLiteralTagText(item.Tag);
				ImGui.TableNextColumn();
				ImGui.TextWrapped(desc);
				ImGui.TableNextColumn();
				if (ImGui.Button($"{Tr("tag.insert", "插入")}##TagSearch_{item.Tag}"))
				{
					InsertTagToFormat(ovText, item.Tag, item.PlaceCursorInside);
				}
			}

			ImGui.EndTable();
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
			DrawLiteralTagText(tag);
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
			DrawLiteralTagText(tag);
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

	private static bool DrawAssetPathEditor(string label, ref string path, string category, bool rebuildFonts, Action afterChanged = null)
	{
		string input = path ?? string.Empty;
		ImGui.SetNextItemWidth(260f);
		bool changed = false;
		bool committed = false;
		if (ImGui.InputText(label, ref input, 512u))
		{
			path = input;
			changed = true;
		}

		if (ImGui.IsItemDeactivatedAfterEdit())
		{
			bool imported = ImportResourcePath(ref path, category, rebuildFonts);
			changed |= imported;
			committed = true;
		}

		ImGui.SameLine();
		if (ImGui.Button(Tr("common.apply", "应用") + "##" + label))
		{
			bool imported = ImportResourcePath(ref path, category, rebuildFonts);
			changed |= imported;
			committed = true;
		}

		ImGui.SameLine();
		if (ImGui.Button(Tr("common.clear", "清空") + "##" + label))
		{
			path = string.Empty;
			changed = true;
			committed = true;
		}

		if (changed)
		{
			Main.RequestSave();
		}

		if (committed)
		{
			afterChanged?.Invoke();
		}

		return changed;
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

	private static string FormatPreviewText(string text)
	{
		return FormatPreviewTags(text);
	}

	private static string FormatPreviewTags(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return text;
		}

		DateTime now = DateTime.Now;

		string result = text
			.Replace("{ttile}", "128")
			.Replace("{atile}", "65")
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
			.Replace("{score}", "456789")
			.Replace("{xperfect:xpp}", "10")
			.Replace("{xperfect:epp}", "2")
			.Replace("{xperfect:lpp}", "1")
			.Replace("{Attempts}", "12")
			.Replace("{Checkpointused}", "1")
			.Replace("{Curcheckpoint}", "2")
			.Replace("{Totalcheckpoint}", "4")
			.Replace("{GameVersion}", Application.version ?? "3.3.1")
			.Replace("{CheryToolsVersion}", Main.ModEntry != null && Main.ModEntry.Info != null ? Main.ModEntry.Info.Version.ToString() : "26.3")
			.Replace("{TotalPlaytime}", FormatPreviewDuration(3661.0))
			.Replace("{MinFPS}", "120")
			.Replace("{MaxFPS}", "240")
			.Replace("{music}", "Artist - SongName")
			.Replace("{artist}", "Artist")
			.Replace("{title}", "SongName")
			.Replace("{level}", "Level Author");

		result = Regex.Replace(result, @"\{fps(?:[:](\d+))?\}", match => FormatPreviewNumber(144.0, match, 0));
		result = Regex.Replace(result, @"\{bpm(?:[:](\d+))?\}", match => FormatPreviewNumber(123.45, match, 2));
		result = Regex.Replace(result, @"\{tbpm(?:[:](\d+))?\}", match => FormatPreviewNumber(185.18, match, 2));
		result = Regex.Replace(result, @"\{cbpm(?:[:](\d+))?\}", match => FormatPreviewNumber(246.9, match, 2));
		result = Regex.Replace(result, @"\{timing(?:[:](\d+))?\}", match => FormatPreviewNumber(-12.0, match, 0));
		result = Regex.Replace(result, @"\{acc(?:[:](\d+))?\}", match => FormatPreviewNumber(98.5, match, 2));
		result = Regex.Replace(result, @"\{xacc(?:[:](\d+))?\}", match => FormatPreviewNumber(97.0, match, 2));
		result = Regex.Replace(result, @"\{progress(?:[:](\d+))?\}", match => FormatPreviewNumber(50.0, match, 2));
		return result;
	}

	private static void ReloadSettingsAfterImport(string sourcePath)
	{
		ReloadSettingsAfterImport(sourcePath, 0, 0);
	}

	private static void ReloadSettingsAfterImport(string sourcePath, int sourceWidth, int sourceHeight)
	{
		Main.Settings = UnityModManager.ModSettings.Load<Settings>(Main.ModEntry);
		Main.Settings.InitNulls();
		Main.Settings.EnsureImGuiPanelScaleBaseline(true);

		// Adapt the imported layout to the local resolution when the package recorded
		// the resolution it was exported at (new-style .cyt with PackageInfo.xml).
		if (sourceWidth > 0 && sourceHeight > 0)
		{
			CheryToolsAssets.TryAdaptSettingsToCurrentResolution(Main.Settings, sourceWidth, sourceHeight);
		}

		// After any import the layout now matches the local resolution, so make the
		// local resolution the new baseline for the runtime resolution watcher.
		int localWidth;
		int localHeight;
		CheryToolsAssets.GetCurrentScreenSizeInternal(out localWidth, out localHeight);
		if (localWidth > 0 && localHeight > 0)
		{
			Main.Settings.LastKnownScreenWidth = localWidth;
			Main.Settings.LastKnownScreenHeight = localHeight;
		}

		LocalizationManager.Reload(Main.Settings.Language);
		if (CheryToolsAssets.ImportSettingsAssets(Main.Settings))
		{
			Main.Settings.Save(Main.ModEntry);
			LocalizationManager.Reload(Main.Settings.Language);
		}
		if ((Object)KeyViewerManager.Instance != (Object)null)
		{
			KeyViewerManager.Instance.RefreshKeys();
		}
		InputInterceptor.UpdateAllowedKeys();
		VideoTextureManager.Shutdown();
		ImGuiController.NeedsFontAtlasRebuild = true;
		OverlayRenderInvalidator.InvalidateAll();
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
			VideoTextureManager.Shutdown();
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

	private void ExportKeyViewerPackage(bool currentOnly)
	{
		try
		{
			KVConfiguration selected = currentOnly ? Main.Settings.GetSelectedKeyViewerConfiguration() : null;
			if (currentOnly && selected == null) throw new InvalidOperationException("没有可导出的 KV 配置。");
			string safeName = currentOnly && !string.IsNullOrWhiteSpace(selected.Name) ? selected.Name : "CheryTools_KeyViewer";
			string path = ModernFileDialog.ShowSaveFileDialog(
				currentOnly ? "导出当前 KeyViewer 配置" : "导出全部 KeyViewer 配置",
				"CheryTools KeyViewer 配置 (*.ctkv)|*.ctkv",
				CheryToolsAssets.GameRoot,
				safeName + ".ctkv"
			);
			
			if (!string.IsNullOrEmpty(path))
			{
				path = currentOnly
					? CheryToolsAssets.ExportKeyViewerPackage(Main.Settings, selected, path)
					: CheryToolsAssets.ExportKeyViewerPackage(Main.Settings, path);
				_keyViewerExportMessage = "已导出: " + path;
				Main.Logger.Log("KeyViewer package exported to: " + path);
			}
			else
			{
				_keyViewerExportMessage = "导出已取消";
			}
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
			string path = ModernFileDialog.ShowOpenFileDialog(
				"导入 KeyViewer 配置",
				"CheryTools KeyViewer 配置 (*.ctkv)|*.ctkv",
				CheryToolsAssets.GameRoot
			);
			
			if (!string.IsNullOrEmpty(path))
			{
				PackageImportResult importResult = CheryToolsAssets.ImportKeyViewerPackage(Main.Settings, path);
				Main.Settings.InitNulls();
				if (CheryToolsAssets.ImportSettingsAssets(Main.Settings))
				{
					_keyViewerExportMessage = "已导入并同步外置资源: " + path;
				}
				else
				{
					_keyViewerExportMessage = "已导入: " + path;
				}
				if (importResult != null)
				{
					_keyViewerExportMessage += "\n" + importResult.ToSummary();
				}
				_selectedKVSidebarTab = Main.Settings.KeyViewerSelectedConfigIndex;
				if ((Object)KeyViewerManager.Instance != (Object)null)
				{
					KeyViewerManager.Instance.RefreshKeys();
				}
				OverlayRenderInvalidator.InvalidateAll();
				InputInterceptor.UpdateAllowedKeys();
			TextureManager.Clear();
			VideoTextureManager.Shutdown();
			ImGuiController.NeedsFontAtlasRebuild = true;
			CheryToolsAssets.UpdateBaselineToCurrentResolution(Main.Settings);
			Main.Settings.Save(Main.ModEntry);
			Main.Logger.Log("KeyViewer package imported from: " + path);
			}
			else
			{
				_keyViewerExportMessage = "导入已取消";
			}
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
			string path = ModernFileDialog.ShowSaveFileDialog(
				"导出 Overlayer 配置",
				"CheryTools Overlayer 配置 (*.ctov)|*.ctov",
				CheryToolsAssets.GameRoot,
				"CheryTools_Overlayer.ctov"
			);
			
			if (!string.IsNullOrEmpty(path))
			{
				path = CheryToolsAssets.ExportOverlayerPackage(Main.Settings, path);
				_overlayerExportMessage = "已导出: " + path;
				Main.Logger.Log("Overlayer package exported to: " + path);
			}
			else
			{
				_overlayerExportMessage = "导出已取消";
			}
		}
		catch (Exception ex)
		{
			_overlayerExportMessage = "OV 导出失败: " + ex.Message;
			Main.Logger.Log("Failed to export Overlayer package: " + ex.ToString());
		}
	}

	private void ExportOverlayerComponentPackage(string kind, int index, string displayName)
	{
		try
		{
			string fileName = string.IsNullOrWhiteSpace(displayName) ? "CheryTools_OV_Component" : displayName;
			string path = ModernFileDialog.ShowSaveFileDialog(
				"导出单个 Overlayer 组件",
				"CheryTools Overlayer 配置 (*.ctov)|*.ctov",
				CheryToolsAssets.GameRoot,
				fileName + ".ctov");
			if (string.IsNullOrEmpty(path))
			{
				_overlayerExportMessage = "导出已取消";
				return;
			}
			path = CheryToolsAssets.ExportOverlayerComponentPackage(Main.Settings, kind, index, path);
			_overlayerExportMessage = "已导出单个组件: " + path;
			Main.Logger.Log("Overlayer component package exported to: " + path);
		}
		catch (Exception ex)
		{
			_overlayerExportMessage = "OV 单组件导出失败: " + ex.Message;
			Main.Logger.Log("Failed to export Overlayer component package: " + ex.ToString());
		}
	}

	private void ImportOverlayerPackage()
	{
		try
		{
			string path = ModernFileDialog.ShowOpenFileDialog(
				"导入 Overlayer 配置",
				"CheryTools Overlayer 配置 (*.ctov)|*.ctov",
				CheryToolsAssets.GameRoot
			);
			
			if (!string.IsNullOrEmpty(path))
			{
				PackageImportResult importResult = CheryToolsAssets.ImportOverlayerPackage(Main.Settings, path);
				Main.Settings.InitNulls();
				if (CheryToolsAssets.ImportSettingsAssets(Main.Settings))
				{
					_overlayerExportMessage = "已导入并同步外置资源: " + path;
				}
				else
				{
					_overlayerExportMessage = "已导入: " + path;
				}
				if (importResult != null)
				{
					_overlayerExportMessage += "\n" + importResult.ToSummary();
				}
				if (importResult != null && importResult.FirstImportedIndex >= 0)
				{
					if (importResult.ImportedComponentKind == "text") _selectedOvSidebarTab = importResult.FirstImportedIndex;
					else if (importResult.ImportedComponentKind == "image") _selectedOvSidebarImgTab = importResult.FirstImportedIndex;
					else if (importResult.ImportedComponentKind == "video") _selectedOvSidebarVideoTab = importResult.FirstImportedIndex;
					else if (importResult.ImportedComponentKind == "progress") _selectedOvSidebarBarTab = importResult.FirstImportedIndex;
				}
			TextureManager.Clear();
			VideoTextureManager.Shutdown();
			SdfTextRenderer.Shutdown();
			ImGuiController.NeedsFontAtlasRebuild = true;
			CheryToolsAssets.UpdateBaselineToCurrentResolution(Main.Settings);
			Main.Settings.Save(Main.ModEntry);
			Main.Logger.Log("Overlayer package imported from: " + path);
			}
			else
			{
				_overlayerExportMessage = "导入已取消";
			}
		}
		catch (Exception ex)
		{
			_overlayerExportMessage = "OV 导入失败: " + ex.Message;
			Main.Logger.Log("Failed to import Overlayer package: " + ex.ToString());
		}
	}


	public static bool IsMenuOpen = false;

	public static bool ShowToolsWindow = true;

	public static bool ShowKeyviewerWindow = false;

	public static bool ShowOverlayerWindow = false;

	private static bool ShowOverlayerTextEditorWindow = false;

	public static bool ShowSettingsWindow = false;

	private static bool ShowHelpWindow = false;

	private int _currentToolTab;

	private enum ControlPanelPage
	{
		Tools,
		KeyViewer,
		Overlayer,
		Settings,
		Help
	}

	private struct ControlPanelLayout
	{
		public Vector2 DisplaySize;
		public float Margin;
		public float TopHeight;
		public float SidebarWidth;
		public Vector2 TopPosition;
		public Vector2 TopSize;
		public Vector2 SidebarPosition;
		public Vector2 SidebarSize;
		public Vector2 HeaderPosition;
		public Vector2 HeaderSize;
		public Vector2 ContentPosition;
		public Vector2 ContentSize;
	}

	private ControlPanelPage _controlPanelPage = ControlPanelPage.Tools;

	private bool _controlPanelPageInitialized;

	private string _controlPanelSearch = "";

	private int _settingsSidebarTab = 0;

	private bool _waitingForToggleMenuKey = false;

	private bool _waitingForToolsLimitedKey = false;

	private bool _editingImGuiPanelScale = false;

	private float _pendingImGuiPanelScale = 1.0f;

	private bool _editingOverlayUpdateRate = false;

	private float _pendingOverlayUpdateRate = 240.0f;

	private bool _editingOverlayerDataUpdateRate = false;

	private float _pendingOverlayerDataUpdateRate = 60.0f;

	private bool _editingKeyViewerKpsRefreshInterval = false;

	private float _pendingKeyViewerKpsRefreshInterval = 0.25f;

	private bool _editingOverlayerFpsTagRefreshInterval = false;

	private float _pendingOverlayerFpsTagRefreshInterval = 0.25f;

	private bool _editingImageRenderScale = false;

	private float _pendingImageRenderScale = 1.0f;

	private string _gameUIDeveloperKeyInput = "";

	private bool _gameUIDeveloperKeyFailed = false;

	private string _legacyKeyViewerImportMessage = "";

	private string _keyViewerExportMessage = "";

	private string _overlayerExportMessage = "";

	private string _languageConfigMessage = "";

	private string _sponsorKeyInput = "";

	private string _sponsorMessage = "";

	

	private int _selectedKVSidebarTab = -1;

	private static int _selectedOvSidebarTab = 0;

	private static int _selectedOvSidebarImgTab = 0;

	private static int _selectedOvSidebarVideoTab = 0;

	private static int _selectedOvSidebarBarTab = 0;

	private static int _overlayerTextEditorIndex = 0;

	private static bool _centerOverlayerTextEditorWindowNextFrame = false;

	private bool _lastGamePlayingForVideoRefresh = false;

	private void Update()
	{
		if (Input.GetKeyDown(Main.Settings.ToggleMenuKey))
		{
			IsMenuOpen = !IsMenuOpen;
		}

		if (VideoTextureManager.HasEntries)
		{
			bool isGamePlaying = Main.IsGamePlaying();
			if (isGamePlaying != _lastGamePlayingForVideoRefresh)
			{
				_lastGamePlayingForVideoRefresh = isGamePlaying;
				VideoTextureManager.RefreshExpectedPlayback();
			}
		}
	}

	private bool DrawColorPicker(string label, ref float[] colorData)
	{
		if (colorData == null || colorData.Length != 4)
		{
			colorData = new float[] { 1f, 1f, 1f, 1f };
		}
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

	private const string ProgressBarPresetCombo =
		"自定义\0" +
		"地图进度 {progress}\0" +
		"音乐播放进度 {musictime:p}/{musictime}\0" +
		"地图游玩进度 {maptime:p}/{maptime}\0" +
		"准度 {acc}\0" +
		"X 准度 {xacc}\0";

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

	private static int GetProgressBarPreset(OverlayerProgressBar bar)
	{
		if (bar == null || bar.ValueSource == null || bar.MinSource == null || bar.MaxSource == null)
		{
			return 0;
		}

		if (bar.ValueSource.Kind == OverlayerProgressValueKind.Progress
			&& IsConstantSource(bar.MinSource, 0.0)
			&& IsConstantSource(bar.MaxSource, 100.0))
		{
			return 1;
		}
		if (bar.ValueSource.Kind == OverlayerProgressValueKind.MusicPlayedTime
			&& IsConstantSource(bar.MinSource, 0.0)
			&& bar.MaxSource.Kind == OverlayerProgressValueKind.MusicTotalTime)
		{
			return 2;
		}
		if (bar.ValueSource.Kind == OverlayerProgressValueKind.MapPlayedTime
			&& IsConstantSource(bar.MinSource, 0.0)
			&& bar.MaxSource.Kind == OverlayerProgressValueKind.MapTotalTime)
		{
			return 3;
		}
		if (bar.ValueSource.Kind == OverlayerProgressValueKind.Accuracy
			&& IsConstantSource(bar.MinSource, 0.0)
			&& IsConstantSource(bar.MaxSource, 100.0))
		{
			return 4;
		}
		if (bar.ValueSource.Kind == OverlayerProgressValueKind.XAccuracy
			&& IsConstantSource(bar.MinSource, 0.0)
			&& IsConstantSource(bar.MaxSource, 100.0))
		{
			return 5;
		}

		return 0;
	}

	private static bool IsConstantSource(OverlayerProgressValueSource source, double value)
	{
		return source != null
			&& source.Kind == OverlayerProgressValueKind.Constant
			&& Math.Abs(source.Constant - value) <= 0.000001;
	}

	private static void ApplyProgressBarPreset(OverlayerProgressBar bar, int preset)
	{
		if (bar == null)
		{
			return;
		}

		if (bar.ValueSource == null) bar.ValueSource = new OverlayerProgressValueSource();
		if (bar.MinSource == null) bar.MinSource = new OverlayerProgressValueSource();
		if (bar.MaxSource == null) bar.MaxSource = new OverlayerProgressValueSource();

		switch (preset)
		{
			case 1:
				bar.ValueSource.Kind = OverlayerProgressValueKind.Progress;
				bar.ValueSource.Constant = 0.0;
				bar.MinSource.Kind = OverlayerProgressValueKind.Constant;
				bar.MinSource.Constant = 0.0;
				bar.MaxSource.Kind = OverlayerProgressValueKind.Constant;
				bar.MaxSource.Constant = 100.0;
				break;
			case 2:
				bar.ValueSource.Kind = OverlayerProgressValueKind.MusicPlayedTime;
				bar.ValueSource.Constant = 0.0;
				bar.MinSource.Kind = OverlayerProgressValueKind.Constant;
				bar.MinSource.Constant = 0.0;
				bar.MaxSource.Kind = OverlayerProgressValueKind.MusicTotalTime;
				bar.MaxSource.Constant = 0.0;
				break;
			case 3:
				bar.ValueSource.Kind = OverlayerProgressValueKind.MapPlayedTime;
				bar.ValueSource.Constant = 0.0;
				bar.MinSource.Kind = OverlayerProgressValueKind.Constant;
				bar.MinSource.Constant = 0.0;
				bar.MaxSource.Kind = OverlayerProgressValueKind.MapTotalTime;
				bar.MaxSource.Constant = 0.0;
				break;
			case 4:
				bar.ValueSource.Kind = OverlayerProgressValueKind.Accuracy;
				bar.ValueSource.Constant = 0.0;
				bar.MinSource.Kind = OverlayerProgressValueKind.Constant;
				bar.MinSource.Constant = 0.0;
				bar.MaxSource.Kind = OverlayerProgressValueKind.Constant;
				bar.MaxSource.Constant = 100.0;
				break;
			case 5:
				bar.ValueSource.Kind = OverlayerProgressValueKind.XAccuracy;
				bar.ValueSource.Constant = 0.0;
				bar.MinSource.Kind = OverlayerProgressValueKind.Constant;
				bar.MinSource.Constant = 0.0;
				bar.MaxSource.Kind = OverlayerProgressValueKind.Constant;
				bar.MaxSource.Constant = 100.0;
				break;
		}
	}

	private void DrawGameUISettings()
	{
		Main.Settings.EnsureGameUIElementSettings();

		ImGui.Text(Tr("tools.gameUi.title", "游戏 UI"));
		ImGui.Separator();
		ImGui.TextWrapped(Tr("tools.gameUi.hint", "仅在关卡游玩界面生效，不接管菜单、选关或关卡编辑器 UI。"));

		bool enabled = Main.Settings.GameUIControlEnabled;
		if (ImGui.Checkbox(Tr("tools.gameUi.enable", "启用游戏 UI 控制"), ref enabled))
		{
			Main.Settings.GameUIControlEnabled = enabled;
			if (!enabled && GameUIManager.Instance != null)
			{
				GameUIManager.Instance.RestoreAll();
			}
			Main.RequestSave();
		}

		ImGui.SameLine();
		if (ImGui.Button(Tr("tools.gameUi.resetAll", "重置全部") + "##GameUIResetAll"))
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
			if (ImGui.CollapsingHeader(TrGameUITarget(target) + "##header"))
			{
				bool controlled = setting.Enabled;
				if (ImGui.Checkbox(Tr("tools.gameUi.control", "接管"), ref controlled))
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
				if (ImGui.Checkbox(Tr("tools.gameUi.visible", "可见"), ref visible))
				{
					setting.Visible = visible;
					Main.RequestSave();
				}

				bool advancedLocked = GameUIManager.IsRestrictedAdvancedTarget(target.Id) && !Main.Settings.GameUIDeveloperUnlocked;
				if (advancedLocked)
				{
					ImGui.TextColored(new System.Numerics.Vector4(1f, 0.78f, 0.35f, 1f), Tr("tools.gameUi.advancedLocked", "开发者选项未解锁：此项仅支持隐藏，位置/缩放/透明度已禁用。"));
					ImGui.BeginDisabled();
				}

				float offsetX = setting.OffsetX;
				ImGui.SetNextItemWidth(120f);
				if (ImGui.DragFloat(Tr("tools.gameUi.positionX", "位置 X"), ref offsetX, 1f, -3000f, 3000f, "%.1f"))
				{
					setting.OffsetX = offsetX;
					Main.RequestSave();
				}

				ImGui.SameLine();
				float offsetY = setting.OffsetY;
				ImGui.SetNextItemWidth(120f);
				if (ImGui.DragFloat(Tr("tools.gameUi.positionY", "位置 Y"), ref offsetY, 1f, -3000f, 3000f, "%.1f"))
				{
					setting.OffsetY = offsetY;
					Main.RequestSave();
				}

				float scale = setting.Scale;
				ImGui.SetNextItemWidth(160f);
				if (ImGui.SliderFloat(Tr("tools.gameUi.scale", "缩放"), ref scale, 0.05f, 5f, "%.2f"))
				{
					setting.Scale = Math.Max(0.05f, Math.Min(5f, scale));
					Main.RequestSave();
				}

				ImGui.SameLine();
				float alpha = setting.Alpha;
				ImGui.SetNextItemWidth(160f);
				if (ImGui.SliderFloat(Tr("tools.gameUi.alpha", "透明度"), ref alpha, 0f, 1f, "%.2f"))
				{
					setting.Alpha = Math.Max(0f, Math.Min(1f, alpha));
					Main.RequestSave();
				}

				if (advancedLocked)
				{
					ImGui.EndDisabled();
				}

				if (ImGui.Button(Tr("tools.gameUi.resetThis", "重置此项") + "##reset"))
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

		string newConfigLabel = Tr("kv.newConfig", "新建配置");
		List<string> configNames = new List<string>(configs.Count);
		for (int i = 0; i < configs.Count; i++)
		{
			KVConfiguration config = configs[i];
			configNames.Add(config == null || string.IsNullOrEmpty(config.Name)
				? "KV 配置 " + (i + 1).ToString()
				: config.Name);
		}
		float configSidebarWidth = CalculateDynamicConfigSidebarWidth(configNames, newConfigLabel, true);

		ImGui.BeginChild("KVConfigSidebar", new Vector2(configSidebarWidth, 0f), ImGuiChildFlags.Borders);
		if (ImGui.Button(newConfigLabel, new Vector2(-1f, 0f)))
		{
			ImGui.OpenPopup("KVCreateConfigPopup");
		}
		if (ImGui.BeginPopup("KVCreateConfigPopup"))
		{
			if (ImGui.MenuItem(Tr("kv.blankConfig", "空白配置"))) AddKeyViewerConfiguration("空白配置 " + (configs.Count + 1).ToString(), 0);
			ImGui.Separator();
			if (ImGui.MenuItem(Tr("kv.from16kPreset", "从 16K 预设开始"))) AddKeyViewerConfiguration("16K 配置 " + (configs.Count + 1).ToString(), 16);
			if (ImGui.MenuItem(Tr("kv.from12kPreset", "从 12K 预设开始"))) AddKeyViewerConfiguration("12K 配置 " + (configs.Count + 1).ToString(), 12);
			if (ImGui.MenuItem(Tr("kv.from10kPreset", "从 10K 预设开始"))) AddKeyViewerConfiguration("10K 配置 " + (configs.Count + 1).ToString(), 10);
			if (ImGui.MenuItem(Tr("kv.from8kPreset", "从 8K 预设开始"))) AddKeyViewerConfiguration("8K 配置 " + (configs.Count + 1).ToString(), 8);
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
			ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1f), Tr("kv.noConfig", "还没有 KV 配置"));
			ImGui.Text(Tr("kv.createHint", "点击左侧“新建配置”开始。"));
			ImGui.EndChild();
			return;
		}

		ImGui.PushID("kv_config_content_" + selectedConfig.GetHashCode().ToString());
		try
		{
			DrawKeyViewerSelectedConfiguration(selectedConfig);
			ImGui.Separator();
			DrawKeyViewerConfigSettings(selectedConfig);
		}
		finally
		{
			ImGui.PopID();
		}
		ImGui.EndChild();
	}

	private static float CalculateDynamicConfigSidebarWidth(
		IEnumerable<string> itemNames,
		string actionLabel,
		bool hasLeadingCheckbox = false)
	{
		float widestText = ImGui.CalcTextSize(actionLabel ?? string.Empty).X;
		if (itemNames != null)
		{
			foreach (string itemName in itemNames)
			{
				float width = ImGui.CalcTextSize(itemName ?? string.Empty).X;
				if (width > widestText)
					widestText = width;
			}
		}

		ImGuiStylePtr style = ImGui.GetStyle();
		float leadingWidth = hasLeadingCheckbox
			? ImGui.GetFrameHeight() + style.ItemSpacing.X
			: 0f;
		float desiredWidth = widestText + leadingWidth + style.WindowPadding.X * 2f + style.FramePadding.X * 2f + 12f;

		// Recalculate from the current list every frame. This lets the sidebar both
		// grow for a newly-created/renamed long item and shrink after it is removed.
		const float minimumWidth = 180f;
		const float absoluteMaximumWidth = 420f;
		float availableWidth = Math.Max(minimumWidth, ImGui.GetContentRegionAvail().X);
		float responsiveMaximum = Math.Max(minimumWidth, Math.Min(absoluteMaximumWidth, availableWidth * 0.42f));
		return Math.Max(minimumWidth, Math.Min(desiredWidth, responsiveMaximum));
	}

	private static void DrawInlineHelpText(string text)
	{
		ImGui.SameLine();
		ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.55f, 0.55f, 0.55f, 1f));
		ImGui.TextWrapped(text);
		ImGui.PopStyleColor();
	}

	private static Vector2 GetImGuiDisplaySize()
	{
		Vector2 displaySize = ImGui.GetIO().DisplaySize;
		if (displaySize.X <= 0f || displaySize.Y <= 0f)
		{
			displaySize = new Vector2(UnityEngine.Screen.width, UnityEngine.Screen.height);
		}
		if (displaySize.X <= 0f) displaySize.X = 1920f;
		if (displaySize.Y <= 0f) displaySize.Y = 1080f;
		return displaySize;
	}

	private static void CenterNextWindowIfRequested(ref bool requested, Vector2 fallbackSize)
	{
		if (!requested)
		{
			return;
		}

		Vector2 displaySize = GetImGuiDisplaySize();
		float width = fallbackSize.X > 0f ? fallbackSize.X : 480f;
		float height = fallbackSize.Y > 0f ? fallbackSize.Y : 360f;
		Vector2 center = new Vector2(Math.Max(0f, (displaySize.X - width) * 0.5f), Math.Max(0f, (displaySize.Y - height) * 0.5f));
		ImGui.SetNextWindowPos(center, ImGuiCond.Always);
		requested = false;
	}

	private void DrawLocalizationSettings()
	{
		ImGui.Text(Tr("settings.localization", "语言设置"));
		string[] languageIds = LocalizationManager.LanguageIds;
		string currentLanguage = LocalizationManager.NormalizeLanguage(Main.Settings.Language);
		int currentIndex = 0;
		for (int i = 0; i < languageIds.Length; i++)
		{
			if (string.Equals(languageIds[i], currentLanguage, StringComparison.OrdinalIgnoreCase))
			{
				currentIndex = i;
				break;
			}
		}

		ImGui.SetNextItemWidth(180f);
		if (ImGui.BeginCombo(Tr("settings.language", "界面语言") + "##LanguageSelect", LocalizationManager.GetDisplayName(languageIds[currentIndex])))
		{
			for (int i = 0; i < languageIds.Length; i++)
			{
				string language = languageIds[i];
				bool selected = i == currentIndex;
				if (ImGui.Selectable(LocalizationManager.GetDisplayName(language) + "##" + language, selected))
				{
					Main.Settings.Language = language;
					LocalizationManager.Reload(language);
					ImGuiController.NeedsFontAtlasRebuild = true;
					_languageConfigMessage = "";
					Main.RequestSave();
				}
				if (selected)
				{
					ImGui.SetItemDefaultFocus();
				}
			}
			ImGui.EndCombo();
		}

		if (ImGui.Button(Tr("settings.createLanguageConfigs", "新建语言配置 JSON")))
		{
			string path = LocalizationManager.CreateMissingLanguageFiles();
			LocalizationManager.Reload(Main.Settings.Language);
			_languageConfigMessage = string.Format(Tr("settings.languageConfigCreated", "语言配置已创建: {0}"), path);
		}
		ImGui.SameLine();
		if (ImGui.Button(Tr("settings.reloadLanguageConfigs", "重新加载语言配置")))
		{
			LocalizationManager.Reload(Main.Settings.Language);
			_languageConfigMessage = Tr("settings.languageConfigReloaded", "语言配置已重新加载");
		}

		ImGui.TextWrapped(string.Format(Tr("settings.languageConfigHint", "语言文件位于 {0}，用户可以直接编辑对应 JSON。"), LocalizationManager.LanguageDirectory));
		if (!string.IsNullOrEmpty(_languageConfigMessage))
		{
			ImGui.TextColored(new Vector4(0.3f, 1f, 0.45f, 1f), _languageConfigMessage);
		}
	}

	private void DrawSettingsSidebarItem(int tabIndex, string localizationKey, string fallback)
	{
		if (ImGui.Selectable(Tr(localizationKey, fallback), _settingsSidebarTab == tabIndex))
		{
			_settingsSidebarTab = tabIndex;
		}
	}

	private void DrawSettingsPanel()
	{
		if (_settingsSidebarTab < 0 || _settingsSidebarTab > 6)
		{
			_settingsSidebarTab = 0;
		}

		ImGui.BeginChild("SettingsContent", new Vector2(0f, 0f), ImGuiChildFlags.Borders);
		switch (_settingsSidebarTab)
		{
			case 0:
				DrawSettingsGeneralSection();
				break;
			case 1:
				DrawLocalizationSettings();
				break;
			case 2:
				DrawSettingsRenderSection();
				break;
			case 3:
				DrawSettingsIntegrationSection();
				break;
			case 4:
				DrawSettingsDeveloperSection();
				break;
			case 5:
				DrawSettingsConfigSection();
				break;
			case 6:
				DrawSponsorSettingsSection();
				break;
		}
		ImGui.EndChild();
	}

	private SponsorRecord GetAuthenticatedSponsor()
	{
		if (Main.Settings == null)
			return null;
		return SponsorManager.FindByHash(Main.Settings.SponsorKeyHash);
	}

	private void DrawSponsorSettingsSection()
	{
		ImGui.Text("赞助者");
		ImGui.Separator();
		ImGui.TextWrapped("请输入赞助者 Key 登录。");
		ImGui.Spacing();

		SponsorRegistryState registryState = SponsorManager.State;
		SponsorRecord[] registrySponsors = SponsorManager.GetSponsorsSnapshot();
		if (registryState == SponsorRegistryState.Ready)
		{
			string revisionText = "赞助者信息已同步 · 修订 "
				+ SponsorManager.RegistryRevision.ToString(CultureInfo.InvariantCulture)
				+ " · " + registrySponsors.Length.ToString(CultureInfo.InvariantCulture) + " 位";
			if (!string.IsNullOrWhiteSpace(SponsorManager.RegistryUpdatedAt))
				revisionText += " · " + SponsorManager.RegistryUpdatedAt;
			ImGui.TextColored(new Vector4(0.45f, 1f, 0.70f, 1f), revisionText);
		}
		else if (registryState == SponsorRegistryState.Failed)
		{
			ImGui.TextColored(new Vector4(1f, 0.48f, 0.48f, 1f), SponsorManager.StatusMessage);
		}
		else
		{
			ImGui.TextDisabled(SponsorManager.StatusMessage);
		}

		bool registryLoading = registryState == SponsorRegistryState.Loading;
		if (registryLoading)
			ImGui.BeginDisabled();
		if (ImGui.Button("更新赞助者信息##SponsorRegistryRefresh"))
		{
			SponsorManager.Refresh();
			registryState = SponsorRegistryState.Loading;
			registrySponsors = Array.Empty<SponsorRecord>();
			_sponsorMessage = "正在重新同步赞助者信息……";
		}
		if (registryLoading)
			ImGui.EndDisabled();
		ImGui.Spacing();

		SponsorRecord sponsor = GetAuthenticatedSponsor();
		if (sponsor != null)
		{
			ImGui.TextColored(new Vector4(1f, 0.78f, 0.28f, 1f), "已登录赞助者: " + sponsor.DisplayName);
			ImGui.SameLine();
			ImGui.TextDisabled("UID " + sponsor.BilibiliUid);
		}
		else
		{
			ImGui.TextColored(new Vector4(0.65f, 0.70f, 0.73f, 1f), "当前未登录赞助者");
		}

		bool canAuthenticate = registryState == SponsorRegistryState.Ready;
		if (!canAuthenticate)
			ImGui.BeginDisabled();
		ImGui.SetNextItemWidth(360f);
		ImGui.InputTextWithHint("##SponsorKey", "输入 Sponsor Key", ref _sponsorKeyInput, 256u, ImGuiInputTextFlags.Password);
		ImGui.SameLine();
		if (ImGui.Button("登录 / 重新登录##SponsorLogin"))
		{
			SponsorRecord verifiedSponsor;
			string hash;
			if (SponsorManager.TryAuthenticate(_sponsorKeyInput, out verifiedSponsor, out hash))
			{
				Main.Settings.SponsorKeyHash = hash;
				_sponsorKeyInput = "";
				_sponsorMessage = "登录成功：" + verifiedSponsor.DisplayName;
				Main.RequestSave();
			}
			else
			{
				_sponsorMessage = "Key 无效，请检查输入内容。";
			}
		}
		if (!canAuthenticate)
			ImGui.EndDisabled();

		bool hasStoredLogin = Main.Settings != null
			&& !string.IsNullOrWhiteSpace(Main.Settings.SponsorKeyHash);
		if (hasStoredLogin)
		{
			ImGui.SameLine();
			if (ImGui.Button("清除登录##SponsorLogout"))
			{
				Main.Settings.SponsorKeyHash = "";
				Main.Settings.SponsorTitleEnabled = false;
				Main.Settings.SponsorCustomTitleEnabled = false;
				_sponsorMessage = "已退出赞助者登录。";
				Main.RequestSave();
				sponsor = null;
			}
		}

		if (!string.IsNullOrEmpty(_sponsorMessage))
		{
			ImGui.TextColored(new Vector4(0.45f, 1f, 0.70f, 1f), _sponsorMessage);
		}

		ImGui.Spacing();
		ImGui.Separator();
		ImGui.Text("赞助者显示选项");
		bool benefitsEnabled = sponsor != null;
		if (!benefitsEnabled)
			ImGui.BeginDisabled();

		bool sponsorTitleEnabled = Main.Settings.SponsorTitleEnabled;
		if (ImGui.Checkbox("赞助者标题显示##SponsorTitleEnabled", ref sponsorTitleEnabled))
		{
			Main.Settings.SponsorTitleEnabled = sponsorTitleEnabled;
			if (sponsorTitleEnabled)
				Main.Settings.SponsorCustomTitleEnabled = false;
			Main.RequestSave();
		}
		ImGui.TextWrapped("将左侧标题替换为赞助者名称，并将副标题显示为 CheryTools 赞助者。 ");

		bool customTitleEnabled = Main.Settings.SponsorCustomTitleEnabled;
		if (ImGui.Checkbox("自定义标题显示##SponsorCustomTitleEnabled", ref customTitleEnabled))
		{
			Main.Settings.SponsorCustomTitleEnabled = customTitleEnabled;
			if (customTitleEnabled)
				Main.Settings.SponsorTitleEnabled = false;
			Main.RequestSave();
		}
		ImGui.TextWrapped("自定义 CheryTools 主标题和版本号，颜色保持原控制面板配色。 ");

		if (customTitleEnabled)
		{
			string customTitle = Main.Settings.SponsorCustomTitle ?? "";
			if (ImGui.InputText("主标题##SponsorCustomTitle", ref customTitle, 128u))
			{
				Main.Settings.SponsorCustomTitle = customTitle;
				Main.RequestSave();
			}
			string customSubtitle = Main.Settings.SponsorCustomSubtitle ?? "";
			if (ImGui.InputText("副标题##SponsorCustomSubtitle", ref customSubtitle, 128u))
			{
				Main.Settings.SponsorCustomSubtitle = customSubtitle;
				Main.RequestSave();
			}
		}

		if (!benefitsEnabled)
			ImGui.EndDisabled();
	}

	private void DrawSettingsGeneralSection()
	{
		ImGui.Text(Tr("settings.tab.general", "常规"));
		ImGui.Separator();

		string hotkeyLabel = _waitingForToggleMenuKey ? Tr("settings.waitingKey", "[等待按键输入...]") : Main.Settings.ToggleMenuKey.ToString();
		ImGui.AlignTextToFramePadding();
		ImGui.Text(Tr("settings.hotkey", "呼出设置快捷键:"));
		ImGui.SameLine();
		if (ImGui.Button($"{hotkeyLabel}##ToggleMenuKeyBtn", new Vector2(120f, 0f)))
		{
			_waitingForToggleMenuKey = true;
		}
		if (_waitingForToggleMenuKey)
		{
			foreach (KeyCode value in SettingsHotkeyCandidates)
			{
				if (!Input.GetKeyDown(value))
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
		if (ImGui.SliderFloat(Tr("settings.imguiScale", "ImGui 面板缩放"), ref imguiPanelScale, 0.6f, 2.0f, "%.2f"))
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

		ImGui.Spacing();
		if (ImGui.Button(Tr("settings.closeMenu", "关闭菜单")))
		{
			IsMenuOpen = false;
		}
	}

	private void DrawSettingsRenderSection()
	{
		ImGui.Text(Tr("settings.tab.render", "渲染刷新"));
		ImGui.Separator();


		ImGui.TextUnformatted(Tr("settings.panelBackdrop", "\u63a7\u5236\u9762\u677f\u80cc\u666f"));
		bool panelBlurEnabled = Main.Settings.ImGuiPanelBlurEnabled;
		if (ImGui.Checkbox(Tr("settings.panelBlurEnabled", "\u542f\u7528\u80cc\u666f\u6a21\u7cca"), ref panelBlurEnabled))
		{
			Main.Settings.ImGuiPanelBlurEnabled = panelBlurEnabled;
			ImGuiPanelBackdrop.SetActive(IsMenuOpen);
			Main.RequestSave();
		}
		if (ImGui.IsItemHovered())
		{
			ImGui.SetTooltip(Tr("settings.panelBlurEnabledHint", "\u4ec5\u5728\u63a7\u5236\u9762\u677f\u6253\u5f00\u65f6\u66f4\u65b0\uff0c\u4e0d\u4f1a\u5bf9\u5168\u5c4f\u753b\u9762\u6dfb\u52a0\u906e\u7f69\u3002"));
		}
		if (panelBlurEnabled)
		{
			int blurStrength = Main.Settings.ImGuiPanelBlurStrength;
			ImGui.SetNextItemWidth(240f);
			if (ImGui.SliderInt(Tr("settings.panelBlurStrength", "\u6a21\u7cca\u5f3a\u5ea6") + "##PanelBlurStrength", ref blurStrength, 1, 20))
			{
				Main.Settings.ImGuiPanelBlurStrength = Math.Max(1, Math.Min(20, blurStrength));
				ImGuiPanelBackdrop.SetActive(IsMenuOpen);
				Main.RequestSave();
			}

			float transitionDuration = Main.Settings.ImGuiPanelBlurTransitionDuration;
			ImGui.SetNextItemWidth(240f);
			if (ImGui.SliderFloat(Tr("settings.panelBlurTransitionDuration", "\u52a8\u753b\u65f6\u957f") + "##PanelBlurTransitionDuration", ref transitionDuration, 0f, 2f, "%.2f s"))
			{
				Main.Settings.ImGuiPanelBlurTransitionDuration = Math.Max(0f, Math.Min(2f, transitionDuration));
				Main.RequestSave();
			}
			if (ImGui.IsItemHovered())
				ImGui.SetTooltip(Tr("settings.panelBlurTransitionDurationHint", "\u8bbe\u4e3a 0 \u53ef\u5173\u95ed\u6a21\u7cca\u8fc7\u6e21\u52a8\u753b\u3002"));

			ImGui.TextUnformatted(Tr("settings.panelBlurTransitionEasing", "\u7f13\u52a8\u7c7b\u578b"));
			string transitionEasing = string.IsNullOrWhiteSpace(Main.Settings.ImGuiPanelBlurTransitionEasing)
				? "smootherstep"
				: Main.Settings.ImGuiPanelBlurTransitionEasing;
			ImGui.SetNextItemWidth(240f);
			if (ImGui.Button(transitionEasing + "##PanelBlurTransitionEasing", new System.Numerics.Vector2(240f, 0f)))
				ImGui.OpenPopup("panel_blur_transition_easing_popup");
			if (DrawEasingSelectorPopup("panel_blur_transition_easing_popup", ref transitionEasing))
			{
				Main.Settings.ImGuiPanelBlurTransitionEasing = transitionEasing;
				Main.RequestSave();
			}
		}
		ImGui.Spacing();
		ImGui.Separator();

		if (!_editingOverlayUpdateRate)
		{
			_pendingOverlayUpdateRate = Main.Settings.OverlayUpdateRate;
		}
		float overlayUpdateRate = _pendingOverlayUpdateRate;
		if (ImGui.SliderFloat(Tr("settings.overlayRefreshRate", "覆盖层刷新率"), ref overlayUpdateRate, 30.0f, 360.0f, "%.0f FPS"))
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
		if (ImGui.IsItemHovered())
		{
			ImGui.SetTooltip(Tr("settings.overlayRefreshRate.tooltip",
				"动画（键雨、按下动画、Token 动画）的刷新率。调高更顺滑，也更吃性能。"));
		}

		if (!_editingOverlayerDataUpdateRate)
		{
			_pendingOverlayerDataUpdateRate = Main.Settings.OverlayerDataUpdateRate;
		}
		float overlayerDataUpdateRate = _pendingOverlayerDataUpdateRate;
		if (ImGui.SliderFloat(Tr("settings.overlayerDataRefreshRate", "OV 数值刷新率"), ref overlayerDataUpdateRate, 15.0f, 360.0f, "%.0f FPS"))
		{
			_pendingOverlayerDataUpdateRate = Math.Max(15.0f, Math.Min(360.0f, overlayerDataUpdateRate));
			_editingOverlayerDataUpdateRate = true;
		}
		if (ImGui.IsItemDeactivatedAfterEdit())
		{
			Main.Settings.OverlayerDataUpdateRate = Math.Max(15.0f, Math.Min(360.0f, _pendingOverlayerDataUpdateRate));
			_editingOverlayerDataUpdateRate = false;
			Main.RequestSave();
		}
		if (ImGui.IsItemHovered())
		{
			ImGui.SetTooltip(Tr("settings.overlayerDataRefreshRate.tooltip",
				"数值文本（准确率、进度、连击等）的刷新率。60 以上肉眼看不出差别，调低可省性能。"));
		}

		if (!_editingKeyViewerKpsRefreshInterval)
		{
			_pendingKeyViewerKpsRefreshInterval = Main.Settings.KeyViewerKpsRefreshInterval;
		}
		float keyViewerKpsRefreshInterval = _pendingKeyViewerKpsRefreshInterval;
		if (ImGui.SliderFloat(Tr("settings.keyViewerKpsRefreshInterval", "KV KPS 刷新间隔"), ref keyViewerKpsRefreshInterval, 0.05f, 2.0f, "%.2f s"))
		{
			_pendingKeyViewerKpsRefreshInterval = Math.Max(0.05f, Math.Min(2.0f, keyViewerKpsRefreshInterval));
			_editingKeyViewerKpsRefreshInterval = true;
		}
		if (ImGui.IsItemDeactivatedAfterEdit())
		{
			Main.Settings.KeyViewerKpsRefreshInterval = Math.Max(0.05f, Math.Min(2.0f, _pendingKeyViewerKpsRefreshInterval));
			_editingKeyViewerKpsRefreshInterval = false;
			Main.RequestSave();
		}

		if (!_editingOverlayerFpsTagRefreshInterval)
		{
			_pendingOverlayerFpsTagRefreshInterval = Main.Settings.OverlayerFpsTagRefreshInterval;
		}
		float overlayerFpsTagRefreshInterval = _pendingOverlayerFpsTagRefreshInterval;
		if (ImGui.SliderFloat(Tr("settings.overlayerFpsTagRefreshInterval", "OV FPS Tag 刷新间隔"), ref overlayerFpsTagRefreshInterval, 0.05f, 2.0f, "%.2f s"))
		{
			_pendingOverlayerFpsTagRefreshInterval = Math.Max(0.05f, Math.Min(2.0f, overlayerFpsTagRefreshInterval));
			_editingOverlayerFpsTagRefreshInterval = true;
		}
		if (ImGui.IsItemDeactivatedAfterEdit())
		{
			Main.Settings.OverlayerFpsTagRefreshInterval = Math.Max(0.05f, Math.Min(2.0f, _pendingOverlayerFpsTagRefreshInterval));
			_editingOverlayerFpsTagRefreshInterval = false;
			Main.RequestSave();
		}

		if (!_editingImageRenderScale)
		{
			_pendingImageRenderScale = Main.Settings.ImageRenderScale;
		}
		float imageRenderScale = _pendingImageRenderScale;
		if (ImGui.SliderFloat(Tr("settings.imageRenderScale", "图片渲染倍率"), ref imageRenderScale, 0.25f, 2.0f, "%.2f"))
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

		ImGui.Spacing();
		bool resolutionAutoAdapt = Main.Settings.ResolutionAutoAdaptEnabled;
		if (ImGui.Checkbox(Tr("settings.resolutionAutoAdapt", "分辨率变化时自动调整 KV/OV 布局"), ref resolutionAutoAdapt))
		{
			Main.Settings.ResolutionAutoAdaptEnabled = resolutionAutoAdapt;
			Main.RequestSave();
		}
		if (ImGui.IsItemHovered())
		{
			ImGui.SetTooltip(Tr("settings.resolutionAutoAdaptHint", "开启后，切换游戏分辨率会按当前布局所占比例自动缩放 KV 按键和 OV 模块的位置与尺寸。导入他人的 .ctkv/.ctov/.cyt 时也会按导出时的分辨率自动适配。"));
		}

		ImGui.Spacing();
		ImGui.Text(Tr("settings.ovBake", "OV 烘焙"));
		bool manualBakeEnabled = Main.Settings.OverlayerManualBakeEnabled;
		if (ImGui.Checkbox(Tr("settings.ovManualBake", "启用 OV 手动烘焙模式"), ref manualBakeEnabled))
		{
			Main.Settings.OverlayerManualBakeEnabled = manualBakeEnabled;
			OverlayerManager.Instance?.SetManualBakeMode(manualBakeEnabled);
			Main.RequestSave();
		}
		if (ImGui.IsItemHovered())
		{
			ImGui.SetTooltip(Tr("settings.ovManualBakeHint", "手动模式会忽略普通设置保存导致的烘焙失效。动态 Tag、判定、Combo 和节点动画仍会实时更新；修改 OV 布局或样式后请重新烘焙。"));
		}

		bool hasOverlayerManager = OverlayerManager.Instance != null;
		if (!hasOverlayerManager)
		{
			ImGui.BeginDisabled();
		}
		if (manualBakeEnabled && ImGui.Button(Tr("settings.ovBakeNow", "烘焙并锁定当前 OV") + "##OvBakeNow"))
		{
			OverlayerManager.Instance?.RequestManualBake();
		}
		if (manualBakeEnabled)
		{
			ImGui.SameLine();
			if (ImGui.Button(Tr("settings.ovBakeUnlock", "解除手动烘焙") + "##OvBakeUnlock"))
			{
				Main.Settings.OverlayerManualBakeEnabled = false;
				OverlayerManager.Instance?.SetManualBakeMode(false);
				Main.RequestSave();
			}
		}
		if (!hasOverlayerManager)
		{
			ImGui.EndDisabled();
		}
	}

	private void DrawSettingsIntegrationSection()
	{
		ImGui.Text(Tr("settings.integration", "联动设置"));
		ImGui.Separator();

		bool xPerfectActive = XPerfectBridge.Active;
		ImGui.TextColored(
			xPerfectActive ? new System.Numerics.Vector4(0.3f, 1f, 0.45f, 1f) : new System.Numerics.Vector4(1f, 0.45f, 0.35f, 1f),
			xPerfectActive ? Tr("settings.xperfectDetected", "XPerfect 已检测到") : Tr("settings.xperfectMissing", "XPerfect 未检测到或未启用")
		);
		ImGui.SameLine();
		if (ImGui.Button(Tr("settings.refreshDetection", "刷新检测") + "##XPerfectRefresh"))
		{
			XPerfectBridge.RefreshDetection();
			xPerfectActive = XPerfectBridge.Active;
		}
		if (!xPerfectActive && Main.Settings.XPerfectIntegrationEnabled)
		{
			Main.Settings.XPerfectIntegrationEnabled = false;
			Main.RequestSave();
		}
		bool xPerfectIntegrationEnabled = xPerfectActive && Main.Settings.XPerfectIntegrationEnabled;
		if (!xPerfectActive)
		{
			ImGui.BeginDisabled();
		}
		if (ImGui.Checkbox("XPerfect##IntegrationXPerfect", ref xPerfectIntegrationEnabled))
		{
			Main.Settings.XPerfectIntegrationEnabled = xPerfectActive && xPerfectIntegrationEnabled;
			Main.RequestSave();
		}
		if (!xPerfectActive)
		{
			ImGui.EndDisabled();
		}
	}

	private void DrawSettingsDeveloperSection()
	{
		ImGui.Text(Tr("settings.developerOptions", "开发者选项"));
		ImGui.Separator();

		bool showPerfHud = Main.Settings.ShowPerfHud;
		if (ImGui.Checkbox(Tr("settings.perfHud", "显示性能监控 HUD（帧时间 / GC 分配）"), ref showPerfHud))
		{
			Main.Settings.ShowPerfHud = showPerfHud;
			Main.RequestSave();
		}
		ImGui.Spacing();

		if (Main.Settings.GameUIDeveloperUnlocked)
		{
			ImGui.TextColored(new System.Numerics.Vector4(0.3f, 1f, 0.45f, 1f), Tr("settings.gameUiAdvancedUnlocked", "Game UI 高级控制已解锁"));
			ImGui.SameLine();
			if (ImGui.Button(Tr("settings.relock", "重新锁定") + "##GameUIDevLock"))
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
			if (ImGui.InputText(Tr("settings.gameUiDevKey", "输入 Key") + "##GameUIDevKey", ref _gameUIDeveloperKeyInput, 32u))
			{
				_gameUIDeveloperKeyFailed = false;
			}
			ImGui.SameLine();
			if (ImGui.Button(Tr("settings.verify", "验证") + "##GameUIDevVerify"))
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
			ImGui.TextColored(new System.Numerics.Vector4(0.75f, 0.75f, 0.75f, 1f), Tr("settings.gameUiDevHint", "用于解锁判定模式/不会失败/自动演奏的位置、缩放和透明度控制。"));
			if (_gameUIDeveloperKeyFailed)
			{
				ImGui.TextColored(new System.Numerics.Vector4(1f, 0.35f, 0.35f, 1f), Tr("settings.keyVerifyFailed", "Key 验证失败"));
			}
		}
	}

	private void DrawSettingsConfigSection()
	{
		ImGui.Text(Tr("settings.tab.config", "配置"));
		ImGui.Separator();

		string text3 = Path.Combine(UnityEngine.Application.dataPath, "../CheryTools_Settings_Backup.xml");
		string cytPath = Path.Combine(UnityEngine.Application.dataPath, "../CheryTools_Settings_Backup.cyt");
		if (ImGui.Button(Tr("settings.exportCyt", "导出配置 (.cyt)")))
		{
			try
			{
				string path = ModernFileDialog.ShowSaveFileDialog(
					"导出 CheryTools 总配置",
					"CheryTools 总配置 (*.cyt)|*.cyt",
					CheryToolsAssets.GameRoot,
					"CheryTools_Settings_Backup.cyt"
				);

				if (!string.IsNullOrEmpty(path))
				{
					Main.Settings.Save(Main.ModEntry);
					cytPath = CheryToolsAssets.ExportCytPackage(Main.Settings, path);
					Main.Logger.Log("Settings exported to: " + cytPath);
				}
			}
			catch (Exception ex)
			{
				Main.Logger.Log("Failed to export settings: " + ex.ToString());
			}
		}
		ImGui.SameLine();
		if (ImGui.Button(Tr("settings.importCyt", "导入配置 (.cyt)")))
		{
			try
			{
				string path = ModernFileDialog.ShowOpenFileDialog(
					"导入 CheryTools 总配置",
					"CheryTools 总配置 (*.cyt)|*.cyt",
					CheryToolsAssets.GameRoot
				);

				if (!string.IsNullOrEmpty(path))
				{
					string destFileName = Path.Combine(Main.ModEntry.Path, "Settings.xml");
					int cytSourceWidth;
					int cytSourceHeight;
					CheryToolsAssets.ImportCytPackage(path, destFileName, out cytSourceWidth, out cytSourceHeight);
					ReloadSettingsAfterImport(path, cytSourceWidth, cytSourceHeight);
				}
			}
			catch (Exception ex2)
			{
				Main.Logger.Log("Failed to import settings: " + ex2.ToString());
			}
		}
		if (ImGui.Button(Tr("settings.importXml", "导入配置 (XML)")))
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
			ImGui.TextColored(new System.Numerics.Vector4(0.5f, 0.5f, 0.5f, 1f), Tr("settings.configSavedInGameRoot", "配置已保存在游戏根目录!"));
		}

		ImGui.Separator();
		ImGui.Text(Tr("settings.legacyKvMigration", "旧 KV 配置迁移"));
		ImGui.TextColored(new System.Numerics.Vector4(0.65f, 0.65f, 0.65f, 1f), Tr("settings.legacyKvMigrationHint", "追加导入旧 16K/12K/10K/8K 为新 KV 配置，不覆盖当前配置。"));
		string currentSettingsPath = Path.Combine(Main.ModEntry.Path, "Settings.xml");
		if (ImGui.Button(Tr("settings.importLegacyFromCurrent", "从当前 Settings.xml 导入旧 KV")))
		{
			ImportLegacyKeyViewerFromXml(currentSettingsPath);
		}
		ImGui.SameLine();
		if (ImGui.Button(Tr("settings.importLegacyFromXml", "从备份 XML 导入旧 KV")))
		{
			ImportLegacyKeyViewerFromXml(text3);
		}
		if (ImGui.Button(Tr("settings.importLegacyFromCyt", "从备份 .cyt 导入旧 KV")))
		{
			ImportLegacyKeyViewerFromCyt(cytPath);
		}
		if (!string.IsNullOrEmpty(_legacyKeyViewerImportMessage))
		{
			ImGui.TextColored(new System.Numerics.Vector4(0.75f, 0.85f, 1f, 1f), _legacyKeyViewerImportMessage);
		}
	}

	private void DrawToolsInputLimitSettings()
	{
		if (Main.Settings.ToolsLimitedKeys == null)
		{
			Main.Settings.ToolsLimitedKeys = new List<KeyCode>();
		}
		if (Main.Settings.ToolsLimitedKeys.Count > Settings.MaxToolsLimitedKeys)
		{
			Main.Settings.ToolsLimitedKeys.RemoveRange(Settings.MaxToolsLimitedKeys, Main.Settings.ToolsLimitedKeys.Count - Settings.MaxToolsLimitedKeys);
		}

		ImGui.Separator();
		ImGui.Text(Tr("tools.inputLimit.title", "按键限制"));
		bool enabled = Main.Settings.ToolsLimitInput;
		if (ImGui.Checkbox(Tr("tools.inputLimit.enable", "启用独立按键限制") + "##tools_limit_input", ref enabled))
		{
			Main.Settings.ToolsLimitInput = enabled;
			InputInterceptor.UpdateAllowedKeys();
			Main.RequestSave();
		}
		DrawInlineHelpText(Tr("tools.inputLimit.help", "启用后只放行下方登记的按键，最多 128 个；菜单热键、Esc、Ctrl、F10 保底放行。"));

		string addButtonLabel = _waitingForToolsLimitedKey ? Tr("tools.inputLimit.waiting", "[等待按键...]") : Tr("tools.inputLimit.add", "添加限制键");
		if (ImGui.Button(addButtonLabel, new Vector2(140f, 0f)))
		{
			_waitingForToolsLimitedKey = true;
		}
		ImGui.SameLine();
		ImGui.Text($"{Main.Settings.ToolsLimitedKeys.Count}/{Settings.MaxToolsLimitedKeys}");
		if (_waitingForToolsLimitedKey)
		{
			foreach (KeyCode value in SettingsHotkeyCandidates)
			{
				if (!Input.GetKeyDown(value))
				{
					continue;
				}

				_waitingForToolsLimitedKey = false;
				if (Main.Settings.ToolsLimitedKeys.Count < Settings.MaxToolsLimitedKeys && !Main.Settings.ToolsLimitedKeys.Contains(value))
				{
					Main.Settings.ToolsLimitedKeys.Add(value);
					InputInterceptor.UpdateAllowedKeys();
					Main.RequestSave();
				}
				break;
			}
		}

		if (Main.Settings.ToolsLimitedKeys.Count > 0)
		{
			if (ImGui.Button(Tr("tools.inputLimit.clear", "清空限制键") + "##tools_limit_clear"))
			{
				Main.Settings.ToolsLimitedKeys.Clear();
				InputInterceptor.UpdateAllowedKeys();
				Main.RequestSave();
			}

			if (ImGui.BeginTable("ToolsLimitKeysTable", 3, ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.RowBg))
			{
				ImGui.TableSetupColumn(Tr("common.index", "序号"), ImGuiTableColumnFlags.WidthFixed, 48f);
				ImGui.TableSetupColumn(Tr("common.key", "按键"), ImGuiTableColumnFlags.WidthStretch);
				ImGui.TableSetupColumn(Tr("common.action", "操作"), ImGuiTableColumnFlags.WidthFixed, 72f);
				ImGui.TableHeadersRow();

				for (int i = 0; i < Main.Settings.ToolsLimitedKeys.Count; i++)
				{
					ImGui.TableNextRow();
					ImGui.TableNextColumn();
					ImGui.Text((i + 1).ToString());
					ImGui.TableNextColumn();
					ImGui.Text(Main.Settings.ToolsLimitedKeys[i].ToString());
					ImGui.TableNextColumn();
					if (ImGui.Button($"{Tr("common.delete", "删除")}##tools_limit_key_{i}"))
					{
						Main.Settings.ToolsLimitedKeys.RemoveAt(i);
						InputInterceptor.UpdateAllowedKeys();
						Main.RequestSave();
						i--;
					}
				}

				ImGui.EndTable();
			}
		}
	}

	private void DrawHelpWindow()
	{
		if (!BeginControlPanelContentWindow("##CheryToolsHelpPanel"))
		{
			EndControlPanelContentWindow();
			return;
		}

		ImGui.TextUnformatted(Tr("help.title", "CheryTools 功能说明书"));
		ImGui.Separator();
		ImGui.PushTextWrapPos(0f);
		ImGui.TextUnformatted(Tr("help.intro", "这里汇总 CheryTools 的主要功能、配置入口和常见注意事项。"));
		ImGui.PopTextWrapPos();

		if (ImGui.CollapsingHeader(Tr("help.quickStart", "快速开始"), ImGuiTreeNodeFlags.DefaultOpen))
		{
			DrawHelpBullet("help.quickStart.1", "顶部栏用于打开 Tools、KV、OV、设置和帮助窗口；窗口开启时对应按钮会高亮。");
			DrawHelpBullet("help.quickStart.2", "Settings 中可以切换语言、调整渲染与联动选项，并导入导出总配置。");
			DrawHelpBullet("help.quickStart.3", "KV 与 OV 都支持独立导入导出分享包，外置字体、图片、视频会一起打包。");
			DrawHelpBullet("help.quickStart.4", "大多数修改会立即生效；完成配置后可在设置中导出 .cyt，或在 KV/OV 面板导出 .ctkv/.ctov。");
		}

		if (ImGui.CollapsingHeader(Tr("help.general", "通用操作"), ImGuiTreeNodeFlags.DefaultOpen))
		{
			DrawHelpBullet("help.general.1", "鼠标悬停在输入框或滑块上时，可以直接滚轮或拖动调整数值。");
			DrawHelpBullet("help.general.2", "按住 Ctrl 点击滑块或拖动条，可以切换为手动输入数值。");
			DrawHelpBullet("help.general.3", "文本输入后按 Enter 或点击其它位置即可提交。");
			DrawHelpBullet("help.general.4", "颜色方块可以打开颜色编辑器，Alpha 控制透明度。");
			DrawHelpBullet("help.general.5", "KV FreeMake 与 OV 文本编辑器仍使用可拖动的独立窗口。");
		}

		if (ImGui.CollapsingHeader(Tr("help.tools", "Tools")))
		{
			DrawHelpBullet("help.tools.1", "优化页提供防弹键：同一个按键在极短间隔内重复输入时，可忽略第二次输入。");
			DrawHelpBullet("help.tools.2", "优化页还可以禁用自动播放空格暂停、禁用游玩时滚轮缩放，并配置独立按键限制。");
			DrawHelpBullet("help.tools.3", "视觉页可以隐藏判定文字，并调整星球、球环、尾巴等游玩视觉元素。");
			DrawHelpBullet("help.tools.4", "游戏 UI 页只影响关卡游玩中的 UI，例如倒计时、准度条和关卡名称。");
			DrawHelpBullet("help.tools.5", "部分游戏 UI 的位置、缩放和透明度属于高级控制，需要在设置中输入开发者 Key 解锁。");
			DrawHelpBullet("help.tools.6", "Tools 功能尽量保持关闭即无额外消耗；遇到异常时可先逐项关闭排查。");
		}

		if (ImGui.CollapsingHeader(Tr("help.kv", "KV")))
		{
			DrawHelpBullet("help.kv.1", "KV 支持多个配置，每个配置都可以单独启用；启用多个配置时会同时显示。");
			DrawHelpBullet("help.kv.2", "新建配置可以从预设开始，也可以创建空白配置；旧版 KV 可通过迁移入口追加导入。");
			DrawHelpBullet("help.kv.3", "综合设置控制整体位置、缩放、字体、按键限制和导入导出。");
			DrawHelpBullet("help.kv.4", "按键设置控制背景、边框、文字、描边、阴影、圆角、深度和图片/视频按键。");
			DrawHelpBullet("help.kv.5", "雨滴设置控制键雨启用、速度、高度、圆角、阴影、羽化和纵横渐变。");
			DrawHelpBullet("help.kv.6", "动画设置控制按下/松开时的缩放、位移、颜色过渡和缓动。");
			DrawHelpBullet("help.kv.7", "KV 视频按键最多启用一个，仅支持 .mp4，并可设置循环、透明度和内容缩放。");
			DrawHelpBullet("help.kv.8", "导出的 .ctkv 会包含 KV 配置和使用到的外置资源，适合分享给其它玩家。");
		}

		if (ImGui.CollapsingHeader(Tr("help.freemake", "FreeMake 编辑器")))
		{
			DrawHelpBullet("help.freemake.1", "左键单击选择按键，Ctrl + 左键可以多选。");
			DrawHelpBullet("help.freemake.2", "双击重叠按键会在重叠按键之间轮换选择。");
			DrawHelpBullet("help.freemake.3", "拖动按键可以移动；多选后拖动任意已选按键会整体移动。");
			DrawHelpBullet("help.freemake.4", "鼠标中键拖动画布，滚轮缩放画布。");
			DrawHelpBullet("help.freemake.5", "按键绑定可以点击后直接按下目标按键，不需要手动输入 KeyCode。");
			DrawHelpBullet("help.freemake.6", "编辑器侧栏会按模式显示可用设置；文本模式、图片/视频模式和普通按键模式的选项不同。");
			DrawHelpBullet("help.freemake.7", "单个或多个按键可以启用独立颜色、渐变、键雨和动画设置。");
		}

		if (ImGui.CollapsingHeader(Tr("help.ov", "OV")))
		{
			DrawHelpBullet("help.ov.1", "OV 支持文本、图片、视频和进度条模块；同类模块之间可用深度控制遮挡顺序。");
			DrawHelpBullet("help.ov.2", "文本模块支持 Tag、富文本颜色/字号、描边、阴影和语法高亮编辑器。");
			DrawHelpBullet("help.ov.3", "Tag 插入窗口支持搜索；例如 {fps}、{progress:2}、{bpm:2}、{acc:2}、{wtime}。");
			DrawHelpBullet("help.ov.4", "数字 Tag 支持 :位数 控制小数显示，并会自动去掉无意义的末尾 0。");
			DrawHelpBullet("help.ov.5", "文本模块可以使用 Token 节点动画，为指定字符或 Tag 配置触发动画和颜色效果。");
			DrawHelpBullet("help.ov.6", "图片和视频模块可以设置位置、尺寸、旋转、锚点、透明度和对齐；视频仅支持 .mp4。");
			DrawHelpBullet("help.ov.7", "进度条可以选择当前值、0% 值和 100% 值来源；适合做音乐播放、地图进度、准确率等条形显示。");
			DrawHelpBullet("help.ov.8", "解锁 OV 拖动后，可以在游戏画面直接拖动模块，并使用吸附线辅助对齐。");
		}

		if (ImGui.CollapsingHeader(Tr("help.assets", "资源与分享")))
		{
			DrawHelpBullet("help.assets.1", "导入外置字体、图片、视频时，资源会复制到游戏目录的 CheryToolsAssets 文件夹。");
			DrawHelpBullet("help.assets.2", ".ctkv 只包含 KV 配置和资源，.ctov 只包含 OV 配置和资源，.cyt 包含总配置和资源。");
			DrawHelpBullet("help.assets.3", "导入他人预设时，会按导出者记录的分辨率自动缩放位置和尺寸。");
			DrawHelpBullet("help.assets.4", "如果覆盖安装后新功能缺失，建议在 UMM 中先卸载旧版，再重新安装新版。");
		}

		if (ImGui.CollapsingHeader(Tr("help.performance", "性能建议")))
		{
			DrawHelpBullet("help.performance.1", "KV/OV 渲染会按刷新率和缓存状态尽量减少无效更新；空闲时避免额外重建。");
			DrawHelpBullet("help.performance.2", "图片渲染倍率会影响显存和清晰度；更改后图片缓存会自动刷新。");
			DrawHelpBullet("help.performance.3", "视频、柔化阴影、复杂渐变和大量模块会增加开销；不用时关闭即可避免对应成本。");
			DrawHelpBullet("help.performance.4", "如果需要做性能对比，请保持同一谱面、同一刷新率、同一 KV/OV 配置和同一游戏状态。");
		}

		if (ImGui.CollapsingHeader(Tr("help.troubleshooting", "常见问题")))
		{
			DrawHelpBullet("help.troubleshooting.1", "语言文件位于 Mod 目录的 Languages 文件夹；可以在设置中生成模板后手动编辑 JSON。");
			DrawHelpBullet("help.troubleshooting.2", "自定义字体或图片不显示时，先确认资源文件仍在 CheryToolsAssets 中，路径也没有被移动或删除。");
			DrawHelpBullet("help.troubleshooting.3", "视频偶发暂停时，确认文件是 .mp4，并尝试减少同时启用的视频模块数量。");
			DrawHelpBullet("help.troubleshooting.4", "如果导入配置后显示异常，先确认导出者和导入者使用的是同一版本安装包。");
		}

		if (ImGui.CollapsingHeader(Tr("help.sponsors", "赞助者 / 鸣谢")))
		{
			ImGui.PushTextWrapPos(0f);
			ImGui.TextUnformatted(Tr("help.sponsors.intro", "感谢以下朋友对 CheryTools 开发的支持。"));
			ImGui.PopTextWrapPos();
			ImGui.Separator();
			ImGui.TextUnformatted("Bilibili");
			SponsorRecord[] sponsors = SponsorManager.GetSponsorsSnapshot();
			if (SponsorManager.State == SponsorRegistryState.Ready)
			{
				for (int i = 0; i < sponsors.Length; i++)
				{
					SponsorRecord sponsor = sponsors[i];
					DrawSponsorEntry(sponsor.DisplayName, sponsor.BilibiliUrl, i + 1);
				}
			}
			else
			{
				ImGui.TextDisabled(SponsorManager.StatusMessage);
			}
		}

		EndControlPanelContentWindow();
	}

	private void DrawKeyViewerSelectedConfiguration(KVConfiguration config)
	{
		ImGui.Text(Tr("kv.currentConfig", "当前配置"));
		ImGui.Separator();

		string name = config.Name ?? "";
		if (ImGui.InputText(Tr("kv.name", "名称") + "##kv_config_name", ref name, 128u))
		{
			config.Name = name;
			Main.RequestSave();
		}

		bool showInGame = config.ShowInGame;
		if (ImGui.Checkbox(Tr("kv.showInGame", "游戏中显示") + "##kv_config_show_in_game", ref showInGame))
		{
			config.ShowInGame = showInGame;
			Main.RequestSave();
		}

		bool onlyShowPlaying = config.OnlyShowPlaying;
		if (ImGui.Checkbox(Tr("kv.onlyShowPlaying", "仅游戏时显示") + "##kv_config_only_show_playing", ref onlyShowPlaying))
		{
			config.OnlyShowPlaying = onlyShowPlaying;
			Main.RequestSave();
		}

		int nodeCount = config.Nodes != null ? config.Nodes.Count : 0;
		ImGui.Text(string.Format(Tr("kv.nodeCount", "节点数量: {0}"), nodeCount));

		if (ImGui.Button(Tr("kv.openEditor", "打开 FreeMake 编辑器"), new Vector2(200f, 30f)))
		{
			FreeMakeEditor.IsOpen = true;
		}
		ImGui.SameLine();
		if (ImGui.Button(Tr("kv.copyConfig", "复制配置"), new Vector2(90f, 30f)))
		{
			Main.Settings.EnsureKeyViewerConfigurations();
			KVConfiguration copied = Main.Settings.CloneKeyViewerConfiguration(config);
			int insertIndex = Math.Max(0, Math.Min(Main.Settings.KeyViewerSelectedConfigIndex + 1, Main.Settings.KeyViewerConfigurations.Count));
			Main.Settings.KeyViewerConfigurations.Insert(insertIndex, copied);
			Main.Settings.KeyViewerSelectedConfigIndex = insertIndex;
			_selectedKVSidebarTab = insertIndex;
			InputInterceptor.UpdateAllowedKeys();
			Main.RequestSave();
			return;
		}

		ImGui.SameLine();
		ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.8f, 0.2f, 0.2f, 1f));
		bool deletePressed = ImGui.Button(Tr("kv.deleteConfig", "删除配置"), new Vector2(90f, 30f));
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

		if ((Object)KeyViewerManager.Instance != (Object)null
			&& ImGui.Button(Tr("kv.resetCurrentStats", "重置当前配置统计"), new Vector2(180f, 28f)))
		{
			KeyViewerManager.Instance.ResetCounts(config);
		}
	}

	private void DrawKeyViewerConfigSettings(KVConfiguration config)
	{
		if (config == null) return;

		ImGui.Text(Tr("kv.appearanceSettings", "当前配置外观设置"));
		ImGui.Separator();

		if (ImGui.CollapsingHeader(Tr("kv.section.general", "综合设置") + "##kv_config_general", ImGuiTreeNodeFlags.DefaultOpen))
		{
		string input = config.FontPath ?? "";
		if (ImGui.InputText(Tr("kv.customFontPath", "自定义字体路径 (如 D:/font.ttf 或 .otf)"), ref input, 256u))
		{
			config.FontPath = input;
			Main.RequestSave();
		}
		if (ImGui.IsItemDeactivatedAfterEdit())
		{
			ImportResourcePath(ref config.FontPath, "Fonts", true);
		}
		if (ImGui.Button(Tr("kv.reloadFont", "重新加载字体")))
		{
			ImportResourcePath(ref config.FontPath, "Fonts", true);
			ImGuiController.NeedsFontAtlasRebuild = true;
		}
		float scale = config.Scale;
		if (ImGui.SliderFloat(Tr("kv.scale", "缩放大小"), ref scale, 0.5f, 3f))
		{
			KvLayoutGeometry.SetScaleAroundCenter(config, scale);
			Main.RequestSave();
		}
		bool hideCountText = config.HideCountText;
		if (ImGui.Checkbox(Tr("kv.hideCountText", "隐藏计数数字") + "##kv_config_hide_count_text", ref hideCountText))
		{
			config.HideCountText = hideCountText;
			Main.RequestSave();
		}
		}

		if (ImGui.CollapsingHeader(Tr("kv.section.key", "按键设置") + "##kv_config_key", ImGuiTreeNodeFlags.DefaultOpen))
		{
		float borderThickness = config.BorderThickness;
		if (ImGui.SliderFloat(Tr("kv.defaultBorderThickness", "配置默认边框粗细"), ref borderThickness, 0f, 5f))
		{
			config.BorderThickness = borderThickness;
			Main.RequestSave();
		}
		ImGui.Spacing();
		ImGui.Text(Tr("kv.textOutline", "文字描边"));
		bool keyTextOutlineEnabled = config.KeyTextOutlineEnabled;
		if (ImGui.Checkbox(Tr("kv.enableKeyTextOutline", "开启按键文字描边") + "##kv_config_key_outline", ref keyTextOutlineEnabled))
		{
			config.KeyTextOutlineEnabled = keyTextOutlineEnabled;
			Main.RequestSave();
		}
		if (keyTextOutlineEnabled)
		{
			ImGui.Indent();
			if (DrawColorPicker(Tr("kv.keyTextOutlineColor", "按键文字描边颜色") + "##kv_config_key_outline_color", ref config.KeyTextOutlineColor))
			{
				Main.RequestSave();
			}
			float keyTextOutlineThickness = config.KeyTextOutlineThickness;
			if (ImGui.DragFloat(Tr("kv.keyTextOutlineThickness", "按键文字描边粗细") + "##kv_config_key_outline_thickness", ref keyTextOutlineThickness, 0.1f, 0f, 8f, "%.1f"))
			{
				config.KeyTextOutlineThickness = keyTextOutlineThickness;
				Main.RequestSave();
			}
			ImGui.Unindent();
		}
		bool countTextOutlineEnabled = config.CountTextOutlineEnabled;
		if (ImGui.Checkbox(Tr("kv.enableCountTextOutline", "开启计数文字描边") + "##kv_config_count_outline", ref countTextOutlineEnabled))
		{
			config.CountTextOutlineEnabled = countTextOutlineEnabled;
			Main.RequestSave();
		}
		if (countTextOutlineEnabled)
		{
			ImGui.Indent();
			if (DrawColorPicker(Tr("kv.countTextOutlineColor", "计数文字描边颜色") + "##kv_config_count_outline_color", ref config.CountTextOutlineColor))
			{
				Main.RequestSave();
			}
			float countTextOutlineThickness = config.CountTextOutlineThickness;
			if (ImGui.DragFloat(Tr("kv.countTextOutlineThickness", "计数文字描边粗细") + "##kv_config_count_outline_thickness", ref countTextOutlineThickness, 0.1f, 0f, 8f, "%.1f"))
			{
				config.CountTextOutlineThickness = countTextOutlineThickness;
				Main.RequestSave();
			}
			ImGui.Unindent();
		}
		ImGui.Spacing();
		ImGui.Text(Tr("kv.textShadow", "文字阴影"));
		bool keyTextShadowEnabled = config.KeyTextShadowEnabled;
		if (ImGui.Checkbox(Tr("kv.enableKeyTextShadow", "开启按键文字阴影") + "##kv_config_key_shadow", ref keyTextShadowEnabled))
		{
			config.KeyTextShadowEnabled = keyTextShadowEnabled;
			Main.RequestSave();
		}
		if (keyTextShadowEnabled)
		{
			ImGui.Indent();
			if (DrawColorPicker(Tr("kv.keyTextShadowColor", "按键文字阴影颜色") + "##kv_config_key_shadow_color", ref config.KeyTextShadowColor))
			{
				Main.RequestSave();
			}
			Vector2 keyShadowOffset = new Vector2(config.KeyTextShadowOffset[0], config.KeyTextShadowOffset[1]);
			if (ImGui.DragFloat2(Tr("kv.keyTextShadowOffset", "按键文字阴影偏移") + "##kv_config_key_shadow_offset", ref keyShadowOffset, 0.1f))
			{
				config.KeyTextShadowOffset[0] = keyShadowOffset.X;
				config.KeyTextShadowOffset[1] = keyShadowOffset.Y;
				Main.RequestSave();
			}
			float keyShadowSoftness = config.KeyTextShadowSoftness;
			if (ImGui.DragFloat(Tr("kv.keyTextShadowSoftness", "按键文字阴影柔度") + "##kv_config_key_shadow_softness", ref keyShadowSoftness, 0.5f, 0f, 64f, "%.1f"))
			{
				config.KeyTextShadowSoftness = Math.Max(0f, keyShadowSoftness);
				Main.RequestSave();
			}
			ImGui.Unindent();
		}
		bool countTextShadowEnabled = config.CountTextShadowEnabled;
		if (ImGui.Checkbox(Tr("kv.enableCountTextShadow", "开启计数文字阴影") + "##kv_config_count_shadow", ref countTextShadowEnabled))
		{
			config.CountTextShadowEnabled = countTextShadowEnabled;
			Main.RequestSave();
		}
		if (countTextShadowEnabled)
		{
			ImGui.Indent();
			if (DrawColorPicker(Tr("kv.countTextShadowColor", "计数文字阴影颜色") + "##kv_config_count_shadow_color", ref config.CountTextShadowColor))
			{
				Main.RequestSave();
			}
			Vector2 countShadowOffset = new Vector2(config.CountTextShadowOffset[0], config.CountTextShadowOffset[1]);
			if (ImGui.DragFloat2(Tr("kv.countTextShadowOffset", "计数文字阴影偏移") + "##kv_config_count_shadow_offset", ref countShadowOffset, 0.1f))
			{
				config.CountTextShadowOffset[0] = countShadowOffset.X;
				config.CountTextShadowOffset[1] = countShadowOffset.Y;
				Main.RequestSave();
			}
			float countShadowSoftness = config.CountTextShadowSoftness;
			if (ImGui.DragFloat(Tr("kv.countTextShadowSoftness", "计数文字阴影柔度") + "##kv_config_count_shadow_softness", ref countShadowSoftness, 0.5f, 0f, 64f, "%.1f"))
			{
				config.CountTextShadowSoftness = Math.Max(0f, countShadowSoftness);
				Main.RequestSave();
			}
			ImGui.Unindent();
		}
		DrawKeyViewerColorSettings(config);
		}

		if (ImGui.CollapsingHeader(Tr("kv.section.rain", "雨滴设置") + "##kv_config_rain", ImGuiTreeNodeFlags.DefaultOpen))
		{
			DrawKeyViewerRainSettings(config);
		}

		if (ImGui.CollapsingHeader(Tr("kv.section.animation", "动画设置") + "##kv_config_animation", ImGuiTreeNodeFlags.DefaultOpen))
		{
			DrawKeyViewerAnimationSettings(config);
		}
	}

	private void DrawKeyViewerColorSettings(KVConfiguration config)
	{
		if (config == null) return;

		ImGui.Spacing();
		ImGui.Text(Tr("kv.colorSettings", "颜色设置"));
		ImGui.Separator();
		ImGui.Text(Tr("kv.background", "背景"));
		if (DrawColorPicker(Tr("kv.normal", "未按下") + "##bg_norm", ref config.ColorBgNormal)) Main.RequestSave();
		ImGui.SameLine();
		if (DrawColorPicker(Tr("kv.pressed", "触发") + "##bg_press", ref config.ColorBgPressed)) Main.RequestSave();
		ImGui.Text(Tr("kv.border", "边框"));
		if (DrawColorPicker(Tr("kv.normal", "未按下") + "##border_norm", ref config.ColorBorderNormal)) Main.RequestSave();
		ImGui.SameLine();
		if (DrawColorPicker(Tr("kv.pressed", "触发") + "##border_press", ref config.ColorBorderPressed)) Main.RequestSave();
		ImGui.Text(Tr("kv.text", "文本"));
		if (DrawColorPicker(Tr("kv.normal", "未按下") + "##txt_norm", ref config.ColorTextNormal)) Main.RequestSave();
		ImGui.SameLine();
		if (DrawColorPicker(Tr("kv.pressed", "触发") + "##txt_press", ref config.ColorTextPressed)) Main.RequestSave();
		ImGui.Text(Tr("kv.bottomStatsText", "底部统计文本"));
		if (DrawColorPicker(Tr("kv.kpsColor", "KPS 颜色"), ref config.ColorKps)) Main.RequestSave();
		ImGui.SameLine();
		if (DrawColorPicker(Tr("kv.totalColor", "Total 颜色"), ref config.ColorTotal)) Main.RequestSave();
	}

	private void DrawKeyViewerAnimationSettings(KVConfiguration config)
	{
		if (config == null) return;

		ImGui.Spacing();
		ImGui.Text(Tr("kv.keyPressAnimation", "按键动画"));
		ImGui.Separator();
		bool keyPressAnimationEnabled = config.KeyPressAnimationEnabled;
		if (ImGui.Checkbox(Tr("kv.enableKeyPressAnimation", "开启按键动画") + "##kv_keypress_anim_enable", ref keyPressAnimationEnabled))
		{
			config.KeyPressAnimationEnabled = keyPressAnimationEnabled;
			Main.RequestSave();
		}
		if (keyPressAnimationEnabled)
		{
			ImGui.Indent();
			float keyPressAnimationDuration = config.KeyPressAnimationDuration;
			if (ImGui.DragFloat(Tr("kv.keyPressAnimationDuration", "动画时长") + "##kv_keypress_anim_duration", ref keyPressAnimationDuration, 0.01f, 0.01f, 2.0f, "%.2f"))
			{
				config.KeyPressAnimationDuration = Math.Max(0.01f, Math.Min(2.0f, keyPressAnimationDuration));
				Main.RequestSave();
			}
			bool keyPressAnimationAffectColors = config.KeyPressAnimationAffectColors;
			if (ImGui.Checkbox(Tr("kv.keyPressAnimationAffectColors", "颜色也使用动画过渡") + "##kv_keypress_anim_color", ref keyPressAnimationAffectColors))
			{
				config.KeyPressAnimationAffectColors = keyPressAnimationAffectColors;
				Main.RequestSave();
			}
			float keyPressAnimationScale = config.KeyPressAnimationScale;
			if (ImGui.DragFloat(Tr("kv.keyPressAnimationScale", "按下缩放") + "##kv_keypress_anim_scale", ref keyPressAnimationScale, 0.01f, 0.2f, 3.0f, "%.2f"))
			{
				config.KeyPressAnimationScale = Math.Max(0.2f, Math.Min(3.0f, keyPressAnimationScale));
				Main.RequestSave();
			}
			float keyPressAnimationOffsetX = config.KeyPressAnimationOffsetX;
			if (ImGui.DragFloat(Tr("kv.keyPressAnimationOffsetX", "按下 X 偏移") + "##kv_keypress_anim_offset_x", ref keyPressAnimationOffsetX, 0.5f, -200f, 200f, "%.1f"))
			{
				config.KeyPressAnimationOffsetX = Math.Max(-200f, Math.Min(200f, keyPressAnimationOffsetX));
				Main.RequestSave();
			}
			float keyPressAnimationOffsetY = config.KeyPressAnimationOffsetY;
			if (ImGui.DragFloat(Tr("kv.keyPressAnimationOffsetY", "按下 Y 偏移") + "##kv_keypress_anim_offset_y", ref keyPressAnimationOffsetY, 0.5f, -200f, 200f, "%.1f"))
			{
				config.KeyPressAnimationOffsetY = Math.Max(-200f, Math.Min(200f, keyPressAnimationOffsetY));
				Main.RequestSave();
			}
			ImGui.Text(Tr("kv.keyPressAnimationEasing", "缓动类型"));
			ImGui.SameLine();
			if (ImGui.Button((config.KeyPressAnimationEasing ?? "ease-out-quad") + "##kv_keypress_anim_easing_btn"))
			{
				ImGui.OpenPopup("kv_keypress_anim_easing_popup");
			}
			string selectedEasing = config.KeyPressAnimationEasing ?? "ease-out-quad";
			if (DrawEasingSelectorPopup("kv_keypress_anim_easing_popup", ref selectedEasing))
			{
				config.KeyPressAnimationEasing = selectedEasing;
				Main.RequestSave();
			}
			ImGui.Unindent();
		}
	}

	private void DrawKeyViewerRainSettings(KVConfiguration config)
	{
		if (config == null) return;

		ImGui.Spacing();
		ImGui.Text(Tr("kv.keyRainSettings", "键雨 (Key Rain) 设置"));
		ImGui.Separator();
		bool enableKeyRain = config.EnableKeyRain;
		if (ImGui.Checkbox(Tr("kv.enableKeyRain", "开启键雨") + "##rain_enable", ref enableKeyRain))
		{
			config.EnableKeyRain = enableKeyRain;
			Main.RequestSave();
		}
		if (!enableKeyRain) return;

		float rainSpeed = config.KeyRainSpeed;
		if (ImGui.SliderFloat(Tr("kv.rainSpeed", "飞行速度") + "##rain_speed", ref rainSpeed, 100f, 2000f))
		{
			config.KeyRainSpeed = rainSpeed;
			Main.RequestSave();
		}
		float rainMaxHeight = config.KeyRainMaxHeight;
		if (ImGui.SliderFloat(Tr("kv.rainMaxHeight", "消失距离") + "##rain_maxh", ref rainMaxHeight, 100f, 1500f))
		{
			config.KeyRainMaxHeight = rainMaxHeight;
			Main.RequestSave();
		}
		float rainYOffsetRow1 = config.KeyRainYOffsetRow1;
		if (ImGui.SliderFloat(Tr("kv.rainRow1Offset", "第一排高度偏移") + "##rain_yoffset1", ref rainYOffsetRow1, -200f, 200f))
		{
			config.KeyRainYOffsetRow1 = rainYOffsetRow1;
			Main.RequestSave();
		}
		float rainYOffsetRow2 = config.KeyRainYOffsetRow2;
		if (ImGui.SliderFloat(Tr("kv.rainRow2Offset", "第二排高度偏移") + "##rain_yoffset2", ref rainYOffsetRow2, -200f, 200f))
		{
			config.KeyRainYOffsetRow2 = rainYOffsetRow2;
			Main.RequestSave();
		}
		int rainFadeMode = config.KeyRainFadeMode;
		if (ImGui.Combo(Tr("kv.rainFadeMode", "消失模式") + "##rain_mode", ref rainFadeMode, Tr("kv.rainFadeModeItems", "高度裁剪 (Clip)\0羽化透明 (Fade)\0")))
		{
			config.KeyRainFadeMode = rainFadeMode;
			Main.RequestSave();
		}
		if (config.KeyRainFadeMode == 1)
		{
			float rainFadeHeight = config.KeyRainFadeHeight;
			if (ImGui.SliderFloat(Tr("kv.rainFadeHeight", "羽化高度") + "##rain_fade_height", ref rainFadeHeight, 0.05f, 3.0f, "%.2f"))
			{
				config.KeyRainFadeHeight = Math.Max(0.05f, Math.Min(3.0f, rainFadeHeight));
				Main.RequestSave();
			}
			float rainFadePower = config.KeyRainFadePower;
			if (ImGui.SliderFloat(Tr("kv.rainFadePower", "羽化程度") + "##rain_fade_power", ref rainFadePower, 0.1f, 5.0f, "%.2f"))
			{
				config.KeyRainFadePower = Math.Max(0.1f, Math.Min(5.0f, rainFadePower));
				Main.RequestSave();
			}
		}
		float rainWidthRatio1 = config.KeyRainWidthRatio1;
		if (ImGui.SliderFloat(Tr("kv.rainRow1WidthRatio", "第一排宽度比例") + "##rain_w1", ref rainWidthRatio1, 0.1f, 1f))
		{
			config.KeyRainWidthRatio1 = rainWidthRatio1;
			Main.RequestSave();
		}
		float rainWidthRatio2 = config.KeyRainWidthRatio2;
		if (ImGui.SliderFloat(Tr("kv.rainRow2WidthRatio", "第二排宽度比例") + "##rain_w2", ref rainWidthRatio2, 0.1f, 1f))
		{
			config.KeyRainWidthRatio2 = rainWidthRatio2;
			Main.RequestSave();
		}
		float rainCornerRadius = config.KeyRainCornerRadius;
		if (ImGui.DragFloat(Tr("kv.rainCornerRadius", "雨滴圆角") + "##rain_corner_radius", ref rainCornerRadius, 0.5f, 0f, 128f, "%.1f"))
		{
			config.KeyRainCornerRadius = Math.Max(0f, Math.Min(128f, rainCornerRadius));
			Main.RequestSave();
		}
		ImGui.Text(Tr("kv.rainColor", "雨滴颜色"));
		if (DrawColorPicker(Tr("kv.rainRow1Color", "第一排颜色") + "##rain_c1", ref config.KeyRainColorRow1)) Main.RequestSave();
		ImGui.SameLine();
		if (DrawColorPicker(Tr("kv.rainRow2Color", "第二排颜色") + "##rain_c2", ref config.KeyRainColorRow2)) Main.RequestSave();
		bool keyRainGradientEnabled = config.KeyRainGradientEnabled;
		if (ImGui.Checkbox(Tr("kv.rainGradient", "启用雨滴颜色渐变") + "##rain_gradient_enable", ref keyRainGradientEnabled))
		{
			config.KeyRainGradientEnabled = keyRainGradientEnabled;
			Main.RequestSave();
		}
		if (keyRainGradientEnabled)
		{
			int keyRainGradientMode = config.KeyRainGradientMode;
			if (ImGui.Combo(Tr("kv.rainGradientMode", "雨滴渐变样式") + "##rain_gradient_mode", ref keyRainGradientMode, Tr("kv.rainGradientModeItems", "UV 渐变\0高度遮罩渐变\0")))
			{
				config.KeyRainGradientMode = Math.Max(0, Math.Min(1, keyRainGradientMode));
				Main.RequestSave();
			}
			if (config.KeyRainGradientMode == 1)
			{
				float rainGradientHeight = config.KeyRainGradientHeight;
				if (ImGui.SliderFloat(Tr("kv.rainGradientHeight", "渐变高度") + "##rain_gradient_height", ref rainGradientHeight, 0.05f, 3.0f, "%.2f"))
				{
					config.KeyRainGradientHeight = Math.Max(0.05f, Math.Min(3.0f, rainGradientHeight));
					Main.RequestSave();
				}
				float rainGradientPower = config.KeyRainGradientPower;
				if (ImGui.SliderFloat(Tr("kv.rainGradientPower", "渐变程度") + "##rain_gradient_power", ref rainGradientPower, 0.1f, 5.0f, "%.2f"))
				{
					config.KeyRainGradientPower = Math.Max(0.1f, Math.Min(5.0f, rainGradientPower));
					Main.RequestSave();
				}
			}
			ImGui.Text(Tr("kv.rainGradientEndColor", "雨滴渐变结束色"));
			if (DrawColorPicker(Tr("kv.rainRow1GradientEndColor", "第一排结束色") + "##rain_grad_end_c1", ref config.KeyRainGradientEndColorRow1)) Main.RequestSave();
			ImGui.SameLine();
			if (DrawColorPicker(Tr("kv.rainRow2GradientEndColor", "第二排结束色") + "##rain_grad_end_c2", ref config.KeyRainGradientEndColorRow2)) Main.RequestSave();
		}
		bool keyRainHorizontalGradientEnabled = config.KeyRainHorizontalGradientEnabled;
		if (ImGui.Checkbox(Tr("kv.rainHorizontalGradient", "启用雨滴横向渐变") + "##rain_horizontal_gradient_enable", ref keyRainHorizontalGradientEnabled))
		{
			config.KeyRainHorizontalGradientEnabled = keyRainHorizontalGradientEnabled;
			Main.RequestSave();
		}
		if (keyRainHorizontalGradientEnabled)
		{
			ImGui.Text(Tr("kv.rainHorizontalGradientEndColor", "雨滴右侧颜色"));
			if (DrawColorPicker(Tr("kv.rainRow1HorizontalEndColor", "第一排右侧色") + "##rain_horizontal_grad_end_c1", ref config.KeyRainHorizontalGradientEndColorRow1)) Main.RequestSave();
			ImGui.SameLine();
			if (DrawColorPicker(Tr("kv.rainRow2HorizontalEndColor", "第二排右侧色") + "##rain_horizontal_grad_end_c2", ref config.KeyRainHorizontalGradientEndColorRow2)) Main.RequestSave();
		}

		ImGui.Spacing();
		bool keyRainShadowEnabled = config.KeyRainShadowEnabled;
		if (ImGui.Checkbox(Tr("kv.enableRainShadow", "开启键雨阴影") + "##rain_shadow_enable", ref keyRainShadowEnabled))
		{
			config.KeyRainShadowEnabled = keyRainShadowEnabled;
			if (keyRainShadowEnabled && config.KeyRainShadowSoftness <= 0.01f)
				config.KeyRainShadowSoftness = 12f;
			Main.RequestSave();
		}
		if (keyRainShadowEnabled)
		{
			if (DrawColorPicker(Tr("kv.shadowColor", "阴影颜色") + "##rain_shadow_color", ref config.KeyRainShadowColor)) Main.RequestSave();

			float shadowOffsetX = config.KeyRainShadowOffset != null && config.KeyRainShadowOffset.Length > 0 ? config.KeyRainShadowOffset[0] : 0f;
			float shadowOffsetY = config.KeyRainShadowOffset != null && config.KeyRainShadowOffset.Length > 1 ? config.KeyRainShadowOffset[1] : 0f;
			if (ImGui.DragFloat(Tr("kv.shadowOffsetX", "阴影偏移 X") + "##rain_shadow_offset_x", ref shadowOffsetX, 0.25f, -64f, 64f, "%.1f"))
			{
				if (config.KeyRainShadowOffset == null || config.KeyRainShadowOffset.Length != 2)
					config.KeyRainShadowOffset = new float[] { 0f, 0f };
				config.KeyRainShadowOffset[0] = shadowOffsetX;
				Main.RequestSave();
			}
			if (ImGui.DragFloat(Tr("kv.shadowOffsetY", "阴影偏移 Y") + "##rain_shadow_offset_y", ref shadowOffsetY, 0.25f, -64f, 64f, "%.1f"))
			{
				if (config.KeyRainShadowOffset == null || config.KeyRainShadowOffset.Length != 2)
					config.KeyRainShadowOffset = new float[] { 0f, 0f };
				config.KeyRainShadowOffset[1] = shadowOffsetY;
				Main.RequestSave();
			}

			float shadowSoftness = config.KeyRainShadowSoftness;
			if (ImGui.DragFloat(Tr("kv.shadowSoftness", "阴影柔度") + "##rain_shadow_softness", ref shadowSoftness, 0.5f, 0f, 64f, "%.1f"))
			{
				config.KeyRainShadowSoftness = Math.Max(0f, Math.Min(64f, shadowSoftness));
				Main.RequestSave();
			}
			float shadowStrength = config.KeyRainShadowStrength;
			if (ImGui.SliderFloat(Tr("kv.shadowStrength", "阴影强度") + "##rain_shadow_strength", ref shadowStrength, 0f, 1f, "%.2f"))
			{
				config.KeyRainShadowStrength = Math.Max(0f, Math.Min(1f, shadowStrength));
				Main.RequestSave();
			}
		}
	}

	private void DrawOverlayerTextEditorWindow()
	{
		if (!ShowOverlayerTextEditorWindow)
		{
			return;
		}
		if (Main.Settings == null || Main.Settings.OverlayerTexts == null || Main.Settings.OverlayerTexts.Count == 0)
		{
			ShowOverlayerTextEditorWindow = false;
			return;
		}

		if (_overlayerTextEditorIndex < 0 || _overlayerTextEditorIndex >= Main.Settings.OverlayerTexts.Count)
		{
			_overlayerTextEditorIndex = Math.Max(0, Math.Min(_selectedOvSidebarTab, Main.Settings.OverlayerTexts.Count - 1));
		}

		OverlayerText overlayerText = Main.Settings.OverlayerTexts[_overlayerTextEditorIndex];
		string titleName = string.IsNullOrEmpty(overlayerText.Name) ? $"文本 {_overlayerTextEditorIndex + 1}" : overlayerText.Name;
		CenterNextWindowIfRequested(ref _centerOverlayerTextEditorWindowNextFrame, new Vector2(980f, 560f));
		ImGui.SetNextWindowSize(new Vector2(980f, 560f), ImGuiCond.FirstUseEver);
		if (!ImGui.Begin($"OV 文本编辑器 - {titleName}##OverlayerTextEditorWindow", ref ShowOverlayerTextEditorWindow))
		{
			ImGui.End();
			return;
		}

		ImGui.TextColored(new Vector4(0.65f, 0.78f, 1f, 1f), "编辑文本内容，并使用“插入 Tag”添加动态信息。");
		ImGui.SameLine();
		if (ImGui.Button("插入 Tag##TextEditorInsertTag"))
		{
			ImGui.OpenPopup("TagSelectorPopup");
		}
		DrawOverlayerTagInsertPopup(overlayerText);
		ImGui.Separator();

		ImGui.BeginChild("OvTextEditorPane", new Vector2(0f, 0f), ImGuiChildFlags.Borders);
		try
		{
			ImGui.Text("文本内容");
			string input = overlayerText.TextFormat ?? string.Empty;
			string editorId = $"TextFormatEditorWindow_{_overlayerTextEditorIndex}";
			if (RichTextCodeEditor.Draw(editorId, ref input, new Vector2(0f, -1f)))
			{
				overlayerText.TextFormat = input;
				Main.RequestSave();
			}
			_lastTextFormatCursorPos = RichTextCodeEditor.GetCursor(editorId, input);
		}
		finally
		{
			ImGui.EndChild();
		}

		ImGui.End();
	}

	private static ControlPanelLayout GetControlPanelLayout()
	{
		Vector2 displaySize = GetImGuiDisplaySize();
		float margin = Math.Max(12f, Math.Min(20f, displaySize.Y * 0.018f));
		float topHeight = 48f;
		float gap = 12f;
		float headerHeight = 54f;
		float sidebarWidth = Math.Max(230f, Math.Min(320f, displaySize.X * 0.17f));
		float sidebarY = margin + topHeight + gap;
		float contentX = margin + sidebarWidth + 14f;
		float contentWidth = Math.Max(360f, displaySize.X - contentX - margin);
		float contentY = sidebarY + headerHeight + 8f;
		return new ControlPanelLayout
		{
			DisplaySize = displaySize,
			Margin = margin,
			TopHeight = topHeight,
			SidebarWidth = sidebarWidth,
			TopPosition = new Vector2(margin, margin),
			TopSize = new Vector2(Math.Max(480f, displaySize.X - margin * 2f), topHeight),
			SidebarPosition = new Vector2(margin, sidebarY),
			SidebarSize = new Vector2(sidebarWidth, Math.Max(260f, displaySize.Y - sidebarY - margin)),
			HeaderPosition = new Vector2(contentX, sidebarY),
			HeaderSize = new Vector2(contentWidth, headerHeight),
			ContentPosition = new Vector2(contentX, contentY),
			ContentSize = new Vector2(
				contentWidth,
				Math.Max(260f, displaySize.Y - contentY - margin))
		};
	}

	private void EnsureControlPanelPage()
	{
		if (_controlPanelPageInitialized)
			return;

		if (ShowKeyviewerWindow) _controlPanelPage = ControlPanelPage.KeyViewer;
		else if (ShowOverlayerWindow) _controlPanelPage = ControlPanelPage.Overlayer;
		else if (ShowSettingsWindow) _controlPanelPage = ControlPanelPage.Settings;
		else if (ShowHelpWindow) _controlPanelPage = ControlPanelPage.Help;
		else _controlPanelPage = ControlPanelPage.Tools;
		_controlPanelPageInitialized = true;
		ActivateControlPanelPage(_controlPanelPage);
	}

	private void ActivateControlPanelPage(ControlPanelPage page)
	{
		_controlPanelPage = page;
		ShowToolsWindow = page == ControlPanelPage.Tools;
		ShowKeyviewerWindow = page == ControlPanelPage.KeyViewer;
		ShowOverlayerWindow = page == ControlPanelPage.Overlayer;
		ShowSettingsWindow = page == ControlPanelPage.Settings;
		ShowHelpWindow = page == ControlPanelPage.Help;
	}

	private bool ControlPanelSearchMatches(params string[] values)
	{
		string query = (_controlPanelSearch ?? string.Empty).Trim();
		if (query.Length == 0)
			return true;

		for (int i = 0; values != null && i < values.Length; i++)
		{
			string value = values[i];
			if (!string.IsNullOrEmpty(value)
				&& value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
				return true;
		}
		return false;
	}

	private static void DrawControlPanelGroupTitle(string title, Vector4 color)
	{
		ImGui.Dummy(new Vector2(0f, 5f));
		ImGui.PushStyleColor(ImGuiCol.Text, color);
		ImGui.TextUnformatted(title);
		ImGui.PopStyleColor();
		ImGui.Dummy(new Vector2(0f, 2f));
	}

	private static bool DrawControlPanelNavItem(string id, string label, bool selected, Vector4 accent, float indent = 0f)
	{
		if (indent > 0f)
			ImGui.Indent(indent);

		Vector2 min = ImGui.GetCursorScreenPos();
		float height = 31f;
		ImGui.PushStyleColor(ImGuiCol.Header, selected ? new Vector4(accent.X, accent.Y, accent.Z, 0.17f) : Vector4.Zero);
		ImGui.PushStyleColor(ImGuiCol.HeaderHovered, new Vector4(accent.X, accent.Y, accent.Z, 0.12f));
		ImGui.PushStyleColor(ImGuiCol.HeaderActive, new Vector4(accent.X, accent.Y, accent.Z, 0.22f));
		bool clicked = ImGui.Selectable("##ControlNav_" + id, selected, ImGuiSelectableFlags.None, new Vector2(0f, height));
		ImGui.PopStyleColor(3);

		ImDrawListPtr draw = ImGui.GetWindowDrawList();
		Vector4 textColor = selected
			? new Vector4(0.96f, 1f, 0.99f, 1f)
			: new Vector4(0.65f, 0.72f, 0.73f, 1f);
		Vector2 textSize = ImGui.CalcTextSize(label);
		Vector2 textPosition = new Vector2(
			min.X + 12f,
			(float)Math.Floor(min.Y + Math.Max(0f, (height - textSize.Y) * 0.5f)));
		draw.AddText(textPosition, ImGui.GetColorU32(textColor), label);

		if (selected)
		{
			draw.AddRectFilled(new Vector2(min.X, min.Y + 4f), new Vector2(min.X + 4f, min.Y + height - 4f), ImGui.GetColorU32(accent));
		}

		if (indent > 0f)
			ImGui.Unindent(indent);
		return clicked;
	}

	private string GetControlPanelTitle()
	{
		switch (_controlPanelPage)
		{
			case ControlPanelPage.Tools:
				return "Tools  /  " + (_currentToolTab == 0 ? Tr("tools.tab.optimization", "优化") : _currentToolTab == 1 ? Tr("tools.tab.visual", "视觉") : Tr("tools.tab.gameUi", "游戏 UI"));
			case ControlPanelPage.KeyViewer:
				return "KeyViewer";
			case ControlPanelPage.Overlayer:
				return "Overlayer";
			case ControlPanelPage.Settings:
				return Tr("settings.title", "设置");
			default:
				return Tr("window.help", "帮助");
		}
	}

	private void DrawControlPanelSidebar()
	{
		Vector4 toolsColor = new Vector4(0.53f, 0.95f, 0.84f, 1f);
		Vector4 kvColor = new Vector4(0.40f, 0.78f, 1f, 1f);
		Vector4 ovColor = new Vector4(0.88f, 0.55f, 0.96f, 1f);
		Vector4 settingsColor = new Vector4(1f, 0.78f, 0.42f, 1f);

		SponsorRecord authenticatedSponsor = GetAuthenticatedSponsor();
		bool sponsorTitleEnabled = authenticatedSponsor != null
			&& Main.Settings.SponsorTitleEnabled
			&& authenticatedSponsor.HasFeature(SponsorManager.SponsorTitleFeature);
		bool customTitleEnabled = authenticatedSponsor != null
			&& Main.Settings.SponsorCustomTitleEnabled;

		string brandTitle = "CheryTools";
		string brandSubtitle = "Fable  26.3 Alpha";
		Vector4 brandTitleColor = new Vector4(0.72f, 1f, 0.93f, 1f);
		Vector4 brandSubtitleColor = new Vector4(0.45f, 0.53f, 0.54f, 1f);
		if (sponsorTitleEnabled)
		{
			brandTitle = authenticatedSponsor.DisplayName;
			brandSubtitle = "CheryTools 赞助者";
			brandTitleColor = new Vector4(1f, 0.78f, 0.28f, 1f);
		}
		else if (customTitleEnabled)
		{
			if (!string.IsNullOrWhiteSpace(Main.Settings.SponsorCustomTitle))
				brandTitle = Main.Settings.SponsorCustomTitle;
			if (!string.IsNullOrWhiteSpace(Main.Settings.SponsorCustomSubtitle))
				brandSubtitle = Main.Settings.SponsorCustomSubtitle;
		}

		ImGui.PushStyleColor(ImGuiCol.Text, brandTitleColor);
		ImGui.TextUnformatted(brandTitle);
		ImGui.PopStyleColor();
		ImGui.PushStyleColor(ImGuiCol.Text, brandSubtitleColor);
		ImGui.TextUnformatted(brandSubtitle);
		ImGui.PopStyleColor();
		ImGui.Dummy(new Vector2(0f, 10f));

		bool showTools = ControlPanelSearchMatches("Tools", "优化", "视觉", "游戏 UI");
		if (showTools)
		{
			DrawControlPanelGroupTitle("Tools", toolsColor);
			if (ControlPanelSearchMatches("优化", "Optimization") && DrawControlPanelNavItem("ToolsOptimization", Tr("tools.tab.optimization", "优化"), _controlPanelPage == ControlPanelPage.Tools && _currentToolTab == 0, toolsColor, 10f))
			{
				_currentToolTab = 0;
				ActivateControlPanelPage(ControlPanelPage.Tools);
			}
			if (ControlPanelSearchMatches("视觉", "Visual") && DrawControlPanelNavItem("ToolsVisual", Tr("tools.tab.visual", "视觉"), _controlPanelPage == ControlPanelPage.Tools && _currentToolTab == 1, toolsColor, 10f))
			{
				_currentToolTab = 1;
				ActivateControlPanelPage(ControlPanelPage.Tools);
			}
			if (ControlPanelSearchMatches("游戏 UI", "Game UI") && DrawControlPanelNavItem("ToolsGameUI", Tr("tools.tab.gameUi", "游戏 UI"), _controlPanelPage == ControlPanelPage.Tools && _currentToolTab == 2, toolsColor, 10f))
			{
				_currentToolTab = 2;
				ActivateControlPanelPage(ControlPanelPage.Tools);
			}
		}

		bool showKv = ControlPanelSearchMatches("KeyViewer", "KV", "按键", "配置");
		if (!showKv && Main.Settings.KeyViewerConfigurations != null)
		{
			for (int i = 0; i < Main.Settings.KeyViewerConfigurations.Count && !showKv; i++)
				showKv = ControlPanelSearchMatches(Main.Settings.KeyViewerConfigurations[i]?.Name);
		}
		if (showKv)
		{
			DrawControlPanelGroupTitle("KeyViewer", kvColor);
			bool noKvConfigurations = Main.Settings.KeyViewerConfigurations == null || Main.Settings.KeyViewerConfigurations.Count == 0;
			if (DrawControlPanelNavItem("KeyViewerHome", Tr("kv.currentConfig", "配置管理"), _controlPanelPage == ControlPanelPage.KeyViewer && noKvConfigurations, kvColor, 10f))
				ActivateControlPanelPage(ControlPanelPage.KeyViewer);
			if (Main.Settings.KeyViewerConfigurations != null)
			{
				for (int i = 0; i < Main.Settings.KeyViewerConfigurations.Count; i++)
				{
					KVConfiguration config = Main.Settings.KeyViewerConfigurations[i];
					string name = config != null && !string.IsNullOrWhiteSpace(config.Name) ? config.Name : "配置 " + (i + 1);
					if (!ControlPanelSearchMatches(name)) continue;
					if (DrawControlPanelNavItem("KVConfig" + i, name, _controlPanelPage == ControlPanelPage.KeyViewer && Main.Settings.KeyViewerSelectedConfigIndex == i, kvColor, 22f))
					{
						Main.Settings.KeyViewerSelectedConfigIndex = i;
						_selectedKVSidebarTab = i;
						ActivateControlPanelPage(ControlPanelPage.KeyViewer);
					}
				}
			}
		}

		if (ControlPanelSearchMatches("Overlayer", "OV", "文本", "图片", "视频", "进度条"))
		{
			DrawControlPanelGroupTitle("Overlayer", ovColor);
			if (DrawControlPanelNavItem("OverlayerHome", Tr("ov.enableSystem", "组件编辑"), _controlPanelPage == ControlPanelPage.Overlayer, ovColor, 10f))
				ActivateControlPanelPage(ControlPanelPage.Overlayer);
		}

		if (ControlPanelSearchMatches("设置", "Settings", "常规", "语言", "渲染", "联动", "开发者", "配置", "赞助者"))
		{
			DrawControlPanelGroupTitle(Tr("settings.title", "设置"), settingsColor);
			string[] labels = { "常规", "语言", "渲染刷新", "联动", "开发者", "配置", "赞助者" };
			for (int i = 0; i < labels.Length; i++)
			{
				if (!ControlPanelSearchMatches(labels[i], "Settings")) continue;
				if (DrawControlPanelNavItem("Settings" + i, labels[i], _controlPanelPage == ControlPanelPage.Settings && _settingsSidebarTab == i, settingsColor, 10f))
				{
					_settingsSidebarTab = i;
					ActivateControlPanelPage(ControlPanelPage.Settings);
				}
			}
		}

		if (ControlPanelSearchMatches("帮助", "Help") && DrawControlPanelNavItem("Help", Tr("window.help", "帮助"), _controlPanelPage == ControlPanelPage.Help, new Vector4(0.72f, 0.75f, 0.78f, 1f)))
			ActivateControlPanelPage(ControlPanelPage.Help);
	}

	private void DrawControlPanelShell()
	{
		ControlPanelLayout layout = GetControlPanelLayout();
		float transitionAlpha = ImGuiPanelBackdrop.PanelVisibility;

		ImGuiWindowFlags fixedFlags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize |
			ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoBringToFrontOnFocus;
		if (!IsMenuOpen)
			fixedFlags |= ImGuiWindowFlags.NoInputs;

		// Top bar: its window is exactly as large as the visible bar, never fullscreen.
		ImGui.SetNextWindowPos(layout.TopPosition, ImGuiCond.Always);
		ImGui.SetNextWindowSize(layout.TopSize, ImGuiCond.Always);
		ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(12f, 8f));
		ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0f);
		ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);
		ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0f, 0f, 0f, 0.30f * transitionAlpha));
		ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(0f, 0f, 0f, 0.52f));
		ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, new Vector4(0.08f, 0.12f, 0.11f, 0.90f));
		ImGui.PushStyleColor(ImGuiCol.FrameBgActive, new Vector4(0.10f, 0.16f, 0.14f, 0.95f));
		ImGui.Begin("##CheryToolsControlPanelTopBar", fixedFlags | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
		try
		{
			const float rightButtonsWidth = 242f;
			ImGui.SetNextItemWidth(Math.Max(220f, ImGui.GetContentRegionAvail().X - rightButtonsWidth));
			ImGui.InputTextWithHint("##ControlPanelSearch", Tr("common.search", "\u641c\u7d22..."), ref _controlPanelSearch, 128u);
			ImGui.SameLine();
			ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.045f, 0.060f, 0.057f, 0.90f));
			ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.12f, 0.22f, 0.19f, 0.92f));
			ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.17f, 0.34f, 0.28f, 0.96f));
			if (ImGui.Button("\u652f\u6301 CheryTools", new Vector2(184f, 0f)))
				ActivateControlPanelPage(ControlPanelPage.Help);
			ImGui.SameLine();
			if (ImGui.Button("X##CloseControlPanel", new Vector2(42f, 0f)))
			{
				Main.Settings.Save(Main.ModEntry);
				Main._isSaveRequested = false;
				IsMenuOpen = false;
			}
			ImGui.PopStyleColor(3);
		}
		finally
		{
			ImGui.End();
			ImGui.PopStyleColor(4);
			ImGui.PopStyleVar(3);
		}

		// Sidebar: a dedicated scrolling window aligned from the top bar to the bottom margin.
		ImGui.SetNextWindowPos(layout.SidebarPosition, ImGuiCond.Always);
		ImGui.SetNextWindowSize(layout.SidebarSize, ImGuiCond.Always);
		ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(14f, 12f));
		ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0f);
		ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);
		ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0f, 0f, 0f, 0.28f * transitionAlpha));
		ImGui.Begin("##CheryToolsControlPanelSidebar", fixedFlags);
		DrawControlPanelSidebar();
		ImGui.End();
		ImGui.PopStyleColor();
		ImGui.PopStyleVar(3);

		// Header: transparent, so there is no second mask above the content panel.
		ImGui.SetNextWindowPos(layout.HeaderPosition, ImGuiCond.Always);
		ImGui.SetNextWindowSize(layout.HeaderSize, ImGuiCond.Always);
		ImGui.SetNextWindowBgAlpha(0f);
		ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(12f, 5f));
		ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0f);
		ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);
		ImGui.Begin("##CheryToolsControlPanelHeader", fixedFlags | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
		Vector2 titleMin = ImGui.GetCursorScreenPos();
		ImGui.GetWindowDrawList().AddRectFilled(
			new Vector2(titleMin.X, titleMin.Y + 2f),
			new Vector2(titleMin.X + 3f, titleMin.Y + 20f),
			ImGui.GetColorU32(new Vector4(0.53f, 0.95f, 0.84f, 1f)));
		ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 10f);
		ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.72f, 1f, 0.93f, 1f));
		ImGui.TextUnformatted(GetControlPanelTitle());
		ImGui.PopStyleColor();
		ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 10f);
		ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.38f, 0.46f, 0.45f, 1f));
		ImGui.TextUnformatted("CheryTools / " + _controlPanelPage.ToString());
		ImGui.PopStyleColor();
		ImGui.End();
		ImGui.PopStyleVar(3);

	}

	private static void PushControlPanelContentTheme()
	{
		ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0.010f, 0.014f, 0.013f, 1f));
		ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0f, 0f, 0f, 0.18f));
		ImGui.PushStyleColor(ImGuiCol.PopupBg, new Vector4(0.025f, 0.032f, 0.030f, 0.98f));
		ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(0.62f, 0.95f, 0.86f, 0.10f));
		ImGui.PushStyleColor(ImGuiCol.Separator, new Vector4(0.62f, 0.95f, 0.86f, 0.10f));
		ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(0f, 0f, 0f, 0.34f));
		ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, new Vector4(0.09f, 0.15f, 0.13f, 0.80f));
		ImGui.PushStyleColor(ImGuiCol.FrameBgActive, new Vector4(0.11f, 0.19f, 0.16f, 0.88f));
		ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0f, 0f, 0f, 0.38f));
		ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.11f, 0.20f, 0.17f, 0.86f));
		ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.18f, 0.38f, 0.31f, 0.95f));
		ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(0.12f, 0.24f, 0.20f, 0.48f));
		ImGui.PushStyleColor(ImGuiCol.HeaderHovered, new Vector4(0.16f, 0.32f, 0.27f, 0.66f));
		ImGui.PushStyleColor(ImGuiCol.HeaderActive, new Vector4(0.19f, 0.40f, 0.33f, 0.82f));
		ImGui.PushStyleColor(ImGuiCol.CheckMark, new Vector4(0.52f, 0.98f, 0.84f, 1f));
		ImGui.PushStyleColor(ImGuiCol.SliderGrab, new Vector4(0.42f, 0.80f, 0.69f, 0.90f));
		ImGui.PushStyleColor(ImGuiCol.SliderGrabActive, new Vector4(0.58f, 1f, 0.86f, 1f));
		ImGui.PushStyleColor(ImGuiCol.Tab, new Vector4(0f, 0f, 0f, 0.34f));
		ImGui.PushStyleColor(ImGuiCol.TabHovered, new Vector4(0.14f, 0.29f, 0.24f, 0.82f));
		ImGui.PushStyleColor(ImGuiCol.TabSelected, new Vector4(0.18f, 0.38f, 0.31f, 0.92f));
		ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.90f, 0.93f, 0.92f, 1f));
		ImGui.PushStyleColor(ImGuiCol.TextDisabled, new Vector4(0.42f, 0.48f, 0.47f, 1f));
		ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(16f, 14f));
		ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0f);
		ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);
		ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 0f);
		ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 2f);
		ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(8f, 7f));
	}

	private static bool BeginControlPanelContentWindow(string id)
	{
		ControlPanelLayout layout = GetControlPanelLayout();
		ImGui.SetNextWindowPos(layout.ContentPosition, ImGuiCond.Always);
		ImGui.SetNextWindowSize(layout.ContentSize, ImGuiCond.Always);
		ImGui.SetNextWindowBgAlpha(0.34f * ImGuiPanelBackdrop.PanelVisibility);
		PushControlPanelContentTheme();
		ImGuiWindowFlags flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize |
			ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoBringToFrontOnFocus;
		if (!IsMenuOpen)
			flags |= ImGuiWindowFlags.NoInputs;
		return ImGui.Begin(id, flags);
	}

	private static void EndControlPanelContentWindow()
	{
		ImGui.End();
		ImGui.PopStyleVar(6);
		ImGui.PopStyleColor(22);
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
		bool shouldDrawControlPanel = IsMenuOpen || ImGuiPanelBackdrop.ShouldRenderPanel;
		if (!shouldDrawControlPanel)
		{
			if (FreeMakeEditor.IsOpen)
				FreeMakeEditor.Draw();
			OvTokenNodeEditor.Draw();
			DrawOverlayerTextEditorWindow();
			return;
		}

		float controlPanelAlpha = Math.Max(0.001f, ImGuiPanelBackdrop.PanelVisibility);
		ImGui.PushStyleVar(ImGuiStyleVar.Alpha, controlPanelAlpha);
		try
		{
		EnsureControlPanelPage();
		DrawControlPanelShell();
		if (ShowToolsWindow)
		{
			if (BeginControlPanelContentWindow("##CheryToolsToolsPanel"))
			{
				ImGui.BeginChild("Content", new Vector2(0f, 0f), ImGuiChildFlags.Borders);
				if (_currentToolTab == 0)
				{
					ImGui.Text(Tr("tools.optimization.title", "优化设置"));
					ImGui.Separator();
					bool disableAutoplaySpacePause = Main.Settings.DisableAutoplaySpacePause;
					if (ImGui.Checkbox(Tr("tools.disableAutoplaySpacePause", "禁用自动播放空格暂停"), ref disableAutoplaySpacePause))
					{
						Main.Settings.DisableAutoplaySpacePause = disableAutoplaySpacePause;
						Main.RequestSave();
					}
					bool disablePlayModeScrollZoom = Main.Settings.DisablePlayModeScrollZoom;
					if (ImGui.Checkbox(Tr("tools.disablePlayModeScrollZoom", "禁用播放时滚轮缩放"), ref disablePlayModeScrollZoom))
					{
						Main.Settings.DisablePlayModeScrollZoom = disablePlayModeScrollZoom;
						Main.RequestSave();
					}
					bool enableEditorLevelLibrary = Main.Settings.EnableEditorLevelLibrary;
					if (ImGui.Checkbox(Tr("tools.enableEditorLevelLibrary", "启用关卡列表"), ref enableEditorLevelLibrary))
					{
						Main.Settings.EnableEditorLevelLibrary = enableEditorLevelLibrary;
						EditorLevelLibraryPanel.RefreshFeatureState();
						Main.RequestSave();
					}
					bool toolsAntiBounceKeys = Main.Settings.ToolsAntiBounceKeys;
					if (ImGui.Checkbox(Tr("tools.antiBounceKeys", "防弹键"), ref toolsAntiBounceKeys))
					{
						Main.Settings.ToolsAntiBounceKeys = toolsAntiBounceKeys;
						InputInterceptor.UpdateAllowedKeys();
						Main.RequestSave();
					}
					if (Main.Settings.ToolsAntiBounceKeys)
					{
						float antiBounceIntervalMs = Main.Settings.ToolsAntiBounceIntervalMs;
						ImGui.SetNextItemWidth(160f);
						if (ImGui.DragFloat(Tr("tools.antiBounceIntervalMs", "防弹间隔 (ms)") + "##ToolsAntiBounceIntervalMs", ref antiBounceIntervalMs, 1f, 1f, 500f, "%.0f"))
						{
							Main.Settings.ToolsAntiBounceIntervalMs = Math.Max(1f, Math.Min(500f, antiBounceIntervalMs));
							Main.RequestSave();
						}
					}
					DrawToolsInputLimitSettings();
				}
				else if (_currentToolTab == 1)
				{
					ImGui.Text(Tr("tools.visual.title", "视觉"));
					ImGui.Separator();
					
					
					ImGui.Separator();

					bool hideHitTextEnabled = Main.Settings.HideHitTextEnabled;
					if (ImGui.Checkbox(Tr("tools.hideHitTextEnabled", "启用判定文字隐藏"), ref hideHitTextEnabled))
					{
						Main.Settings.HideHitTextEnabled = hideHitTextEnabled;
						Main.RequestSave();
					}
					if (Main.Settings.HideHitTextEnabled)
					{
						if (ImGui.Button(Tr("tools.hideAllHitText", "隐藏全部判定文字")))
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
						if (ImGui.Button(Tr("tools.showAllHitText", "显示全部判定文字")))
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
					if (ImGui.Checkbox(Tr("tools.customPlanetColors", "启用自定义星球颜色"), ref v2))
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
					ImGui.Text(Tr("tools.planetColorSettings", "星球颜色设置"));
					ImGui.Separator();
					bool customPlanetTextures = Main.Settings.EnableCustomPlanetTextures;
					if (ImGui.Checkbox(Tr("tools.customPlanetTextures", "启用自定义星球贴图"), ref customPlanetTextures))
					{
						Main.Settings.EnableCustomPlanetTextures = customPlanetTextures;
						Main.RequestSave();
						if (customPlanetTextures)
						{
							VisualTweaks.ApplyCustomColors();
						}
						else
						{
							VisualTweaks.RestoreDefaultColors();
							if (Main.Settings.EnableCustomPlanetColors)
							{
								VisualTweaks.ApplyCustomColors();
							}
						}
					}
					ImGui.Text(Tr("tools.firePlanet", "火之行星"));
					ImGui.SameLine(100f);
					if (DrawColorPicker("##RedPlanetColor", ref Main.Settings.RedPlanetColor))
					{
						Main.RequestSave();
						VisualTweaks.ApplyCustomColors();
					}
					ImGui.SameLine();
					ImGui.Text(Tr("tools.ring", "球环"));
					ImGui.SameLine(200f);
					if (DrawColorPicker("##RedRingColor", ref Main.Settings.RedRingColor))
					{
						Main.RequestSave();
						VisualTweaks.ApplyCustomColors();
					}
					ImGui.SameLine();
					ImGui.Text(Tr("tools.tail", "拖尾"));
					ImGui.SameLine(300f);
					if (DrawColorPicker("##RedTailColor", ref Main.Settings.RedTailColor))
					{
						Main.RequestSave();
						VisualTweaks.ApplyCustomColors();
					}
					DrawAssetPathEditor(Tr("tools.planetTexture", "星球贴图") + "##RedPlanetTexture", ref Main.Settings.RedPlanetTexturePath, "Images", false, () =>
					{
						TextureManager.Clear();
						VisualTweaks.ApplyCustomColors();
					});
					ImGui.Text(Tr("tools.icePlanet", "冰之行星"));
					ImGui.SameLine(100f);
					if (DrawColorPicker("##BluePlanetColor", ref Main.Settings.BluePlanetColor))
					{
						Main.RequestSave();
						VisualTweaks.ApplyCustomColors();
					}
					ImGui.SameLine();
					ImGui.Text(Tr("tools.ring", "球环"));
					ImGui.SameLine(200f);
					if (DrawColorPicker("##BlueRingColor", ref Main.Settings.BlueRingColor))
					{
						Main.RequestSave();
						VisualTweaks.ApplyCustomColors();
					}
					ImGui.SameLine();
					ImGui.Text(Tr("tools.tail", "拖尾"));
					ImGui.SameLine(300f);
					if (DrawColorPicker("##BlueTailColor", ref Main.Settings.BlueTailColor))
					{
						Main.RequestSave();
						VisualTweaks.ApplyCustomColors();
					}
					DrawAssetPathEditor(Tr("tools.planetTexture", "星球贴图") + "##BluePlanetTexture", ref Main.Settings.BluePlanetTexturePath, "Images", false, () =>
					{
						TextureManager.Clear();
						VisualTweaks.ApplyCustomColors();
					});
					ImGui.Text(Tr("tools.greenPlanet", "风之行星"));
					ImGui.SameLine(100f);
					if (DrawColorPicker("##GreenPlanetColor", ref Main.Settings.GreenPlanetColor))
					{
						Main.RequestSave();
						VisualTweaks.ApplyCustomColors();
					}
					ImGui.SameLine();
					ImGui.Text(Tr("tools.ring", "球环"));
					ImGui.SameLine(200f);
					if (DrawColorPicker("##GreenRingColor", ref Main.Settings.GreenRingColor))
					{
						Main.RequestSave();
						VisualTweaks.ApplyCustomColors();
					}
					ImGui.SameLine();
					ImGui.Text(Tr("tools.tail", "拖尾"));
					ImGui.SameLine(300f);
					if (DrawColorPicker("##GreenTailColor", ref Main.Settings.GreenTailColor))
					{
						Main.RequestSave();
						VisualTweaks.ApplyCustomColors();
					}
					DrawAssetPathEditor(Tr("tools.planetTexture", "星球贴图") + "##GreenPlanetTexture", ref Main.Settings.GreenPlanetTexturePath, "Images", false, () =>
					{
						TextureManager.Clear();
						VisualTweaks.ApplyCustomColors();
					});
				}
				else if (_currentToolTab == 2)
				{
					DrawGameUISettings();
				}
				ImGui.EndChild();
			}
			EndControlPanelContentWindow();
		}
		if (ShowKeyviewerWindow)
		{
			if (BeginControlPanelContentWindow("##CheryToolsKeyViewerPanel"))
			{
				bool v3 = Main.Settings.EnableKeyViewer;
				if (ImGui.Checkbox(Tr("kv.enable", "开启按键显示悬浮窗"), ref v3))
				{
					Main.Settings.EnableKeyViewer = v3;
					Main.RequestSave();
				}
				ImGui.SameLine();
				bool v4 = Main.Settings.KeyViewerOnlyShowPlaying;
				if (ImGui.Checkbox(Tr("kv.onlyShowPlaying", "仅游戏时显示") + "##kvplay", ref v4))
				{
					Main.Settings.KeyViewerOnlyShowPlaying = v4;
					Main.Logger?.Log($"[CheryTools] KV OnlyShowPlaying changed to: {v4}");
					Main.RequestSave();
				}
				bool v5 = Main.Settings.LimitInput;
				if (ImGui.Checkbox(Tr("kv.limitInput", "限制输入"), ref v5))
				{
					Main.Settings.LimitInput = v5;
					Main.RequestSave();
					InputInterceptor.UpdateAllowedKeys();
				}
				if (ImGui.Button(Tr("kv.exportCurrent", "导出当前配置")))
				{
					ExportKeyViewerPackage(true);
				}
				ImGui.SameLine();
				if (ImGui.Button(Tr("kv.exportAll", "导出全部配置"))) ExportKeyViewerPackage(false);
				ImGui.SameLine();
				if (ImGui.Button(Tr("kv.import", "导入 KV 配置 (.ctkv)")))
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
			EndControlPanelContentWindow();
		}
		if (ShowOverlayerWindow)
		{
			if (BeginControlPanelContentWindow("##CheryToolsOverlayerPanel"))
			{
				bool v21 = Main.Settings.OverlayerSystemEnabled;
				if (ImGui.Checkbox(Tr("ov.enableSystem", "开启 Overlayer 系统"), ref v21))
				{
					Main.Settings.OverlayerSystemEnabled = v21;
					Main.RequestSave();
				}
				ImGui.SameLine();
				bool v22 = Main.Settings.OverlayerOnlyShowPlaying;
				if (ImGui.Checkbox(Tr("ov.onlyShowPlaying", "仅游戏时显示") + "##ovplay", ref v22))
				{
					Main.Settings.OverlayerOnlyShowPlaying = v22;
					Main.Logger?.Log($"[CheryTools] OV OnlyShowPlaying changed to: {v22}");
					Main.RequestSave();
				}
				ImGui.SameLine(ImGui.GetWindowWidth() - 120f);
				bool v23 = Main.Settings.OverlayerEditMode;
				if (ImGui.Checkbox(Tr("ov.unlockDrag", "解锁拖动"), ref v23))
				{
					Main.Settings.OverlayerEditMode = v23;
					Main.RequestSave();
				}
				if (ImGui.Button(Tr("ov.exportAll", "导出全部 OV")))
				{
					ExportOverlayerPackage();
				}
				ImGui.SameLine();
				if (ImGui.Button(Tr("ov.exportSingle", "导出单个组件"))) ImGui.OpenPopup("OVExportSinglePopup");
				if (ImGui.BeginPopup("OVExportSinglePopup"))
				{
					for (int i = 0; Main.Settings.OverlayerTexts != null && i < Main.Settings.OverlayerTexts.Count; i++)
					{
						OverlayerText item = Main.Settings.OverlayerTexts[i];
						string name = item != null && !string.IsNullOrWhiteSpace(item.Name) ? item.Name : "文本 " + (i + 1);
						if (ImGui.MenuItem("文本 / " + name + "##ov_export_text_" + i)) ExportOverlayerComponentPackage("text", i, name);
					}
					for (int i = 0; Main.Settings.OverlayerImages != null && i < Main.Settings.OverlayerImages.Count; i++)
						if (ImGui.MenuItem("图片 / 图片 " + (i + 1) + "##ov_export_image_" + i)) ExportOverlayerComponentPackage("image", i, "图片 " + (i + 1));
					for (int i = 0; Main.Settings.OverlayerVideos != null && i < Main.Settings.OverlayerVideos.Count; i++)
					{
						OverlayerVideo item = Main.Settings.OverlayerVideos[i];
						string name = item != null && !string.IsNullOrWhiteSpace(item.Name) ? item.Name : "视频 " + (i + 1);
						if (ImGui.MenuItem("视频 / " + name + "##ov_export_video_" + i)) ExportOverlayerComponentPackage("video", i, name);
					}
					for (int i = 0; Main.Settings.OverlayerProgressBars != null && i < Main.Settings.OverlayerProgressBars.Count; i++)
					{
						OverlayerProgressBar item = Main.Settings.OverlayerProgressBars[i];
						string name = item != null && !string.IsNullOrWhiteSpace(item.Name) ? item.Name : "进度条 " + (i + 1);
						if (ImGui.MenuItem("进度条 / " + name + "##ov_export_progress_" + i)) ExportOverlayerComponentPackage("progress", i, name);
					}
					ImGui.EndPopup();
				}
				ImGui.SameLine();
				if (ImGui.Button(Tr("ov.import", "导入 OV 配置 (.ctov)")))
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
					if (ImGui.BeginTabItem(Tr("ov.tab.texts", "文本 (Texts)")))
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
						string newTextLabel = Tr("ov.newText", "新建文本 (+)");
						List<string> textNames = new List<string>(overlayerTexts.Count);
						for (int i = 0; i < overlayerTexts.Count; i++)
						{
							OverlayerText item = overlayerTexts[i];
							textNames.Add(item == null || string.IsNullOrEmpty(item.Name) ? $"未命名 {i + 1}" : item.Name);
						}
						float textSidebarWidth = CalculateDynamicConfigSidebarWidth(textNames, newTextLabel);
						ImGui.BeginChild("OvSidebar", new Vector2(textSidebarWidth, 0f), ImGuiChildFlags.Borders);
						if (ImGui.Button(newTextLabel, new Vector2(-1f, 0f)))
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
								ImGui.SameLine();
								if (ImGui.Button("复制##copy_text", new Vector2(60f, 24f)))
								{
									OverlayerText copied = Main.Settings.CloneOverlayerText(overlayerText2);
									overlayerTexts.Insert(selectedOvSidebarTab + 1, copied);
									_selectedOvSidebarTab = selectedOvSidebarTab + 1;
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
								ImGui.SameLine();
								bool textShowInGame = overlayerText2.ShowInGame;
								if (ImGui.Checkbox("游戏中显示##text_show_in_game", ref textShowInGame))
								{
									overlayerText2.ShowInGame = textShowInGame;
									Main.RequestSave();
								}
								bool textOnlyShowPlaying = overlayerText2.OnlyShowPlaying;
								if (ImGui.Checkbox(Tr("ov.onlyShowPlaying", "仅游戏时显示") + "##text_only_show_playing", ref textOnlyShowPlaying))
								{
									overlayerText2.OnlyShowPlaying = textOnlyShowPlaying;
									Main.RequestSave();
								}
								ImGui.Separator();
								ImGui.Spacing();
								if (overlayerText2.IsEnabled)
								{
									ImGui.AlignTextToFramePadding();
									ImGui.Text("\u516C\u5F0F");
									ImGui.SameLine();
									if (ImGui.Button("打开文本编辑器##open_text_editor"))
									{
										_overlayerTextEditorIndex = selectedOvSidebarTab;
										_selectedOvSidebarTab = selectedOvSidebarTab;
										ShowOverlayerTextEditorWindow = true;
										_centerOverlayerTextEditorWindowNextFrame = true;
									}
									ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), "内容: " + SingleLinePreview(FormatPreviewText(overlayerText2.TextFormat ?? string.Empty), 72));
									ImGui.TextColored(new System.Numerics.Vector4(0.5f, 0.5f, 0.5f, 1f), "可用变量: {fps}, {score}, {p}, {te}, {acc}, {progress}...");
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
									ImGui.Text("Token 节点动画");
									if (overlayerText2.TokenAnimation == null) overlayerText2.TokenAnimation = OvAnimationGraph.CreateDefault();
									bool tokenAnimationEnabled = overlayerText2.TokenAnimation.Enabled;
									if (ImGui.Checkbox("启用 Token 节点动画##ov_token_animation_enabled", ref tokenAnimationEnabled))
									{
										overlayerText2.TokenAnimation.Enabled = tokenAnimationEnabled;
										Main.RequestSave();
									}
									ImGui.SameLine();
									if (ImGui.Button("打开节点编辑器##ov_token_node_editor"))
									{
										OvTokenNodeEditor.Open(overlayerText2);
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
					if (ImGui.BeginTabItem(Tr("ov.tab.images", "图片 (Images)")))
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
						string newImageLabel = Tr("ov.newImage", "新建图片 (+)");
						List<string> imageNames = new List<string>(overlayerImages.Count);
						for (int i = 0; i < overlayerImages.Count; i++)
							imageNames.Add($"图片 {i + 1}");
						float imageSidebarWidth = CalculateDynamicConfigSidebarWidth(imageNames, newImageLabel);
						ImGui.BeginChild("OvImgSidebar", new Vector2(imageSidebarWidth, 0f), ImGuiChildFlags.Borders);
						if (ImGui.Button(newImageLabel, new Vector2(-1f, 0f)))
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
								ImGui.SameLine();
								if (ImGui.Button("复制##copy_image", new Vector2(60f, 24f)))
								{
									OverlayerImage copied = Main.Settings.CloneOverlayerImage(overlayerImage);
									overlayerImages.Insert(selectedOvSidebarImgTab + 1, copied);
									_selectedOvSidebarImgTab = selectedOvSidebarImgTab + 1;
									Main.RequestSave();
								}
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
									ImGui.SameLine();
									bool imageShowInGame = overlayerImage.ShowInGame;
									if (ImGui.Checkbox("游戏中显示##image_show_in_game", ref imageShowInGame))
									{
										overlayerImage.ShowInGame = imageShowInGame;
										Main.RequestSave();
									}
									bool imageOnlyShowPlaying = overlayerImage.OnlyShowPlaying;
									if (ImGui.Checkbox(Tr("ov.onlyShowPlaying", "仅游戏时显示") + "##image_only_show_playing", ref imageOnlyShowPlaying))
									{
										overlayerImage.OnlyShowPlaying = imageOnlyShowPlaying;
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
										ImGui.Text("节点动画");
										if (overlayerImage.NodeAnimation == null) overlayerImage.NodeAnimation = OvImageNodeAnimation.CreateDefault();
										bool imageNodeAnimationEnabled = overlayerImage.NodeAnimation.Enabled;
										if (ImGui.Checkbox("启用节点动画##image_node_animation_enabled", ref imageNodeAnimationEnabled))
										{
											overlayerImage.NodeAnimation.Enabled = imageNodeAnimationEnabled;
											Main.RequestSave();
										}
										ImGui.SameLine();
										if (ImGui.Button("打开节点编辑器##image_node_animation"))
										{
											OvTokenNodeEditor.Open(overlayerImage);
										}
										ImGui.TextDisabled("图片输入节点固定代表当前图片，无需选择 Token。");

										ImGui.Separator();
										ImGui.Text("旧版高级动画配置");
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
					if (ImGui.BeginTabItem(Tr("ov.tab.videos", "视频")))
					{
						if (Main.Settings.OverlayerVideos == null)
						{
							Main.Settings.OverlayerVideos = new List<OverlayerVideo>();
						}

						List<OverlayerVideo> overlayerVideos = Main.Settings.OverlayerVideos;
						if (overlayerVideos.Count > 2)
						{
							overlayerVideos.RemoveRange(2, overlayerVideos.Count - 2);
							Main.RequestSave();
						}
						if (_selectedOvSidebarVideoTab >= overlayerVideos.Count)
						{
							_selectedOvSidebarVideoTab = overlayerVideos.Count - 1;
						}
						if (_selectedOvSidebarVideoTab < 0 && overlayerVideos.Count > 0)
						{
							_selectedOvSidebarVideoTab = 0;
						}

						string newVideoLabel = Tr("ov.newVideo", "新建视频 (+)");
						List<string> videoNames = new List<string>(overlayerVideos.Count);
						for (int i = 0; i < overlayerVideos.Count; i++)
						{
							OverlayerVideo item = overlayerVideos[i];
							videoNames.Add(item == null || string.IsNullOrEmpty(item.Name) ? $"视频 {i + 1}" : item.Name);
						}
						float videoSidebarWidth = CalculateDynamicConfigSidebarWidth(videoNames, newVideoLabel);
						ImGui.BeginChild("OvVideoSidebar", new Vector2(videoSidebarWidth, 0f), ImGuiChildFlags.Borders);
						bool canAddVideo = overlayerVideos.Count < 2;
						if (!canAddVideo) ImGui.BeginDisabled();
						if (ImGui.Button(newVideoLabel, new Vector2(-1f, 0f)))
						{
							var video = new OverlayerVideo();
							video.Name = $"新视频 {overlayerVideos.Count + 1}";
							overlayerVideos.Add(video);
							_selectedOvSidebarVideoTab = overlayerVideos.Count - 1;
							Main.RequestSave();
						}
						if (!canAddVideo) ImGui.EndDisabled();
						if (!canAddVideo)
						{
							ImGui.TextColored(new Vector4(0.75f, 0.75f, 0.75f, 1f), Tr("ov.maxVideos", "最多 2 个视频模块"));
						}
						ImGui.Separator();
						for (int v = 0; v < overlayerVideos.Count; v++)
						{
							OverlayerVideo sidebarVideo = overlayerVideos[v];
							string videoName = sidebarVideo != null && !string.IsNullOrEmpty(sidebarVideo.Name) ? sidebarVideo.Name : $"视频 {v + 1}";
							if (ImGui.Selectable($"{videoName}##ov_videotab_{v}", _selectedOvSidebarVideoTab == v))
							{
								_selectedOvSidebarVideoTab = v;
							}
						}
						ImGui.EndChild();
						ImGui.SameLine();

						ImGui.BeginChild("OvVideoContent", new Vector2(0f, 0f), ImGuiChildFlags.Borders);
						if (_selectedOvSidebarVideoTab >= 0 && _selectedOvSidebarVideoTab < overlayerVideos.Count)
						{
							int selectedOvSidebarVideoTab = _selectedOvSidebarVideoTab;
							if (overlayerVideos[selectedOvSidebarVideoTab] == null)
							{
								overlayerVideos[selectedOvSidebarVideoTab] = new OverlayerVideo();
							}

							OverlayerVideo video = overlayerVideos[selectedOvSidebarVideoTab];
							ImGui.PushID($"ov_videoblock_{selectedOvSidebarVideoTab}");
							try
							{
								ImGui.Text($"视频配置 ({selectedOvSidebarVideoTab + 1})");
								ImGui.SameLine(ImGui.GetWindowWidth() - 80f);
								ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.8f, 0.2f, 0.2f, 1f));
								bool deletePressed = ImGui.Button("删除", new Vector2(60f, 24f));
								ImGui.PopStyleColor();

								if (deletePressed)
								{
									overlayerVideos.RemoveAt(selectedOvSidebarVideoTab);
									Main.RequestSave();
									if (_selectedOvSidebarVideoTab >= overlayerVideos.Count)
									{
										_selectedOvSidebarVideoTab = overlayerVideos.Count - 1;
									}
								}
								else
								{
									string videoNameInput = video.Name ?? "";
									if (ImGui.InputText("名称", ref videoNameInput, 128u))
									{
										video.Name = videoNameInput;
										Main.RequestSave();
									}

									bool enabled = video.IsEnabled;
									if (ImGui.Checkbox("启用此视频", ref enabled))
									{
										video.IsEnabled = enabled;
										Main.RequestSave();
									}
									ImGui.SameLine();
									bool showInGame = video.ShowInGame;
									if (ImGui.Checkbox("游戏中显示##video_show_in_game", ref showInGame))
									{
										video.ShowInGame = showInGame;
										Main.RequestSave();
									}
									bool videoOnlyShowPlaying = video.OnlyShowPlaying;
									if (ImGui.Checkbox(Tr("ov.onlyShowPlaying", "仅游戏时显示") + "##video_only_show_playing", ref videoOnlyShowPlaying))
									{
										video.OnlyShowPlaying = videoOnlyShowPlaying;
										Main.RequestSave();
									}

									ImGui.Separator();
									string videoPathInput = video.VideoPath ?? "";
									if (ImGui.InputText("视频绝对路径 (.mp4)", ref videoPathInput, 512u))
									{
										video.VideoPath = videoPathInput;
										Main.RequestSave();
									}
									if (ImGui.IsItemDeactivatedAfterEdit())
									{
										ImportResourcePath(ref video.VideoPath, "Videos", false);
									}
									bool loop = true;
									ImGui.BeginDisabled();
									ImGui.Checkbox("循环播放", ref loop);
									ImGui.EndDisabled();

									ImGui.Separator();
									ImGui.Text("位置与尺寸");
									float videoPosX = video.PositionX;
									float videoPosY = video.PositionY;
									ImGui.SetNextItemWidth(120f);
									if (ImGui.DragFloat("X 位置##videoPosX", ref videoPosX, 1f, -2000f, 4000f))
									{
										video.PositionX = videoPosX;
										Main.RequestSave();
									}
									ImGui.SameLine();
									ImGui.SetNextItemWidth(120f);
									if (ImGui.DragFloat("Y 位置##videoPosY", ref videoPosY, 1f, -2000f, 4000f))
									{
										video.PositionY = videoPosY;
										Main.RequestSave();
									}

									float videoWidth = video.Width;
									float videoHeight = video.Height;
									ImGui.SetNextItemWidth(120f);
									if (ImGui.DragFloat("宽度##videoWidth", ref videoWidth, 1f, 16f, 4000f, "%.1f"))
									{
										video.Width = Math.Max(16f, videoWidth);
										Main.RequestSave();
									}
									ImGui.SameLine();
									ImGui.SetNextItemWidth(120f);
									if (ImGui.DragFloat("高度##videoHeight", ref videoHeight, 1f, 16f, 4000f, "%.1f"))
									{
										video.Height = Math.Max(16f, videoHeight);
										Main.RequestSave();
									}

									float videoRotation = video.Rotation;
									ImGui.SetNextItemWidth(180f);
									if (ImGui.SliderFloat("旋转角度##videoRotation", ref videoRotation, -360f, 360f, "%.1f"))
									{
										video.Rotation = videoRotation;
										Main.RequestSave();
									}

									float videoOpacity = video.Opacity;
									ImGui.SetNextItemWidth(180f);
									if (ImGui.SliderFloat("不透明度##videoOpacity", ref videoOpacity, 0f, 1f, "%.2f"))
									{
										video.Opacity = Math.Max(0f, Math.Min(1f, videoOpacity));
										Main.RequestSave();
									}

									int videoDepth = RenderDepth.ClampDepth(video.Depth);
									ImGui.SetNextItemWidth(180f);
									if (ImGui.SliderInt("\u6DF1\u5EA6##ovVideoDepth", ref videoDepth, RenderDepth.MinDepth, RenderDepth.MaxDepth))
									{
										video.Depth = RenderDepth.ClampDepth(videoDepth);
										Main.RequestSave();
									}

									float videoPX = video.PivotX;
									float videoPY = video.PivotY;
									ImGui.SetNextItemWidth(120f);
									if (ImGui.SliderFloat("锚点 X (Pivot)##videoPivotX", ref videoPX, 0f, 1f, "%.2f"))
									{
										video.PivotX = videoPX;
										Main.RequestSave();
									}
									ImGui.SameLine();
									ImGui.SetNextItemWidth(120f);
									if (ImGui.SliderFloat("锚点 Y (Pivot)##videoPivotY", ref videoPY, 0f, 1f, "%.2f"))
									{
										video.PivotY = videoPY;
										Main.RequestSave();
									}

									ImGui.Text("快速对齐:");
									ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(2f, 2f));
									ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(4f, 4f));
									Vector2 videoBtnSize = new Vector2(28f, 28f);
									Vector2 displaySize = ImGuiController.ScreenDisplaySize;
									if (ImGui.Button("↖##video_tl", videoBtnSize)) AlignOvVideo(video, 0, displaySize); ImGui.SameLine();
									if (ImGui.Button("↑##video_tc", videoBtnSize)) AlignOvVideo(video, 1, displaySize); ImGui.SameLine();
									if (ImGui.Button("↗##video_tr", videoBtnSize)) AlignOvVideo(video, 2, displaySize);
									if (ImGui.Button("←##video_ml", videoBtnSize)) AlignOvVideo(video, 3, displaySize); ImGui.SameLine();
									if (ImGui.Button("┼##video_cc", videoBtnSize)) AlignOvVideo(video, 4, displaySize); ImGui.SameLine();
									if (ImGui.Button("→##video_mr", videoBtnSize)) AlignOvVideo(video, 5, displaySize);
									if (ImGui.Button("↙##video_bl", videoBtnSize)) AlignOvVideo(video, 6, displaySize); ImGui.SameLine();
									if (ImGui.Button("↓##video_bc", videoBtnSize)) AlignOvVideo(video, 7, displaySize); ImGui.SameLine();
									if (ImGui.Button("↘##video_br", videoBtnSize)) AlignOvVideo(video, 8, displaySize);
									ImGui.PopStyleVar(2);
								}
							}
							finally
							{
								ImGui.PopID();
							}
						}
						else
						{
							ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1f), "请在左侧新建或选择一个视频模块");
						}
						ImGui.EndChild();
						ImGui.EndTabItem();
					}
					if (ImGui.BeginTabItem(Tr("ov.tab.progressBars", "进度条")))
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

						string newProgressBarLabel = Tr("ov.newProgressBar", "新建进度条 (+)");
						List<string> progressBarNames = new List<string>(progressBars.Count);
						for (int i = 0; i < progressBars.Count; i++)
						{
							OverlayerProgressBar item = progressBars[i];
							progressBarNames.Add(item == null || string.IsNullOrEmpty(item.Name) ? $"进度条 {i + 1}" : item.Name);
						}
						float progressBarSidebarWidth = CalculateDynamicConfigSidebarWidth(progressBarNames, newProgressBarLabel);
						ImGui.BeginChild("OvBarSidebar", new Vector2(progressBarSidebarWidth, 0f), ImGuiChildFlags.Borders);
						if (ImGui.Button(newProgressBarLabel, new Vector2(-1f, 0f)))
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
								ImGui.SameLine();
								if (ImGui.Button("复制##copy_progress", new Vector2(60f, 24f)))
								{
									OverlayerProgressBar copied = Main.Settings.CloneOverlayerProgressBar(progressBar);
									progressBars.Insert(selectedOvSidebarBarTab + 1, copied);
									_selectedOvSidebarBarTab = selectedOvSidebarBarTab + 1;
									Main.RequestSave();
								}
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
									ImGui.SameLine();
									bool barShowInGame = progressBar.ShowInGame;
									if (ImGui.Checkbox("游戏中显示##bar_show_in_game", ref barShowInGame))
									{
										progressBar.ShowInGame = barShowInGame;
										Main.RequestSave();
									}
									bool barOnlyShowPlaying = progressBar.OnlyShowPlaying;
									if (ImGui.Checkbox(Tr("ov.onlyShowPlaying", "仅游戏时显示") + "##bar_only_show_playing", ref barOnlyShowPlaying))
									{
										progressBar.OnlyShowPlaying = barOnlyShowPlaying;
										Main.RequestSave();
									}

									ImGui.Separator();
									ImGui.Text("数值映射");
									int progressPreset = GetProgressBarPreset(progressBar);
									ImGui.SetNextItemWidth(320f);
									if (ImGui.Combo("进度预设##bar_progress_preset", ref progressPreset, ProgressBarPresetCombo))
									{
										ApplyProgressBarPreset(progressBar, progressPreset);
										Main.RequestSave();
									}
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
									bool enableFillGradient = progressBar.EnableFillGradient;
									if (ImGui.Checkbox("填充颜色随进度变化##barFillGradient", ref enableFillGradient))
									{
										progressBar.EnableFillGradient = enableFillGradient;
										Main.RequestSave();
									}
									if (enableFillGradient)
									{
										if (progressBar.FillGradientStartColor == null || progressBar.FillGradientStartColor.Length != 4)
											progressBar.FillGradientStartColor = new float[] { 1f, 0.25f, 0.25f, 0.95f };
										if (progressBar.FillGradientEndColor == null || progressBar.FillGradientEndColor.Length != 4)
											progressBar.FillGradientEndColor = new float[] { 0.25f, 1f, 0.35f, 0.95f };
										if (DrawColorPicker("0% 填充颜色##barFillGradientStart", ref progressBar.FillGradientStartColor)) Main.RequestSave();
										if (DrawColorPicker("100% 填充颜色##barFillGradientEnd", ref progressBar.FillGradientEndColor)) Main.RequestSave();
									}
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
			EndControlPanelContentWindow();
		}
		if (ShowHelpWindow)
		{
			DrawHelpWindow();
		}
		if (ShowSettingsWindow)
		{
			if (BeginControlPanelContentWindow("##CheryToolsSettingsPanel"))
			{
				DrawSettingsPanel();
			}
			EndControlPanelContentWindow();
		}
		}
		finally
		{
			ImGui.PopStyleVar();
		}

		DrawOverlayerTextEditorWindow();
		if (FreeMakeEditor.IsOpen)
			FreeMakeEditor.Draw();
		OvTokenNodeEditor.Draw();

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

	private static void AlignOvVideo(OverlayerVideo video, int type, Vector2 displaySize)
	{
		if (video == null) return;

		switch (type)
		{
			case 0:
				video.PositionX = 20f;
				video.PositionY = 20f;
				video.PivotX = 0f;
				video.PivotY = 0f;
				break;
			case 1:
				video.PositionX = displaySize.X * 0.5f;
				video.PositionY = 20f;
				video.PivotX = 0.5f;
				video.PivotY = 0f;
				break;
			case 2:
				video.PositionX = displaySize.X - 20f;
				video.PositionY = 20f;
				video.PivotX = 1f;
				video.PivotY = 0f;
				break;
			case 3:
				video.PositionX = 20f;
				video.PositionY = displaySize.Y * 0.5f;
				video.PivotX = 0f;
				video.PivotY = 0.5f;
				break;
			case 4:
				video.PositionX = displaySize.X * 0.5f;
				video.PositionY = displaySize.Y * 0.5f;
				video.PivotX = 0.5f;
				video.PivotY = 0.5f;
				break;
			case 5:
				video.PositionX = displaySize.X - 20f;
				video.PositionY = displaySize.Y * 0.5f;
				video.PivotX = 1f;
				video.PivotY = 0.5f;
				break;
			case 6:
				video.PositionX = 20f;
				video.PositionY = displaySize.Y - 20f;
				video.PivotX = 0f;
				video.PivotY = 1f;
				break;
			case 7:
				video.PositionX = displaySize.X * 0.5f;
				video.PositionY = displaySize.Y - 20f;
				video.PivotX = 0.5f;
				video.PivotY = 1f;
				break;
			case 8:
				video.PositionX = displaySize.X - 20f;
				video.PositionY = displaySize.Y - 20f;
				video.PivotX = 1f;
				video.PivotY = 1f;
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
		
		string selectedKey = (selectedEasing ?? string.Empty).ToLowerInvariant().Replace("-", "").Replace(" ", "");
		string nameKey = (name ?? string.Empty).ToLowerInvariant().Replace("-", "").Replace(" ", "");
		bool isSelected = selectedKey == nameKey;
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

	internal static bool DrawEasingSelectorPopup(string popupId, ref string selectedEasing)
	{
		string oldEasing = selectedEasing;
		ImGui.SetNextWindowSize(new System.Numerics.Vector2(710f, 520f), ImGuiCond.Appearing);
		if (ImGui.BeginPopup(popupId))
		{
			ImGui.Text("选择缓动类型");
			ImGui.Separator();

			string[] easingNames = new string[]
			{
				"linear", "smoothstep", "smootherstep",
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
				DrawEasingCell(easeName, easeName, ref selectedEasing, cellW, cellH);

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

		return !string.Equals(oldEasing, selectedEasing, StringComparison.Ordinal);
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
