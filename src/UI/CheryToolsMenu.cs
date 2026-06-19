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
	private static string _tagSearchText = "";
	private static readonly Regex ColorTagBoundaryRegex = new Regex(@"<\s*(/)?\s*color\s*(?:=\s*[^>]*?)?\s*>", RegexOptions.Compiled | RegexOptions.IgnoreCase);

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
		new TagInsertItem("#import regex\n#regex /pattern/replacement/\n", "启用 OV 正则替换"),
		new TagInsertItem("#regex /(\\d+)\\.0+/$1/\n", "正则示例：去掉数字末尾 .0"),
		new TagInsertItem("{fps}", "当前 FPS"),
		new TagInsertItem("{fps:1}", "当前 FPS，最多 1 位小数"),
		new TagInsertItem("{kps}", "当前每秒按键数"),
		new TagInsertItem("{tot}", "总按键次数"),
		new TagInsertItem("{combo}", "当前 Pure Combo"),
		new TagInsertItem("{combo:p}", "当前 Perfect Combo"),
		new TagInsertItem("{music}", "当前曲目标题"),
		new TagInsertItem("{ttile}", "总轨道数量"),
		new TagInsertItem("{atile}", "经过的轨道数量"),
		new TagInsertItem("{level}", "关卡制作者"),
		new TagInsertItem("{x}", "关卡倍速"),
		new TagInsertItem("{xperfect:xpp}", "XPerfect / XPurePerfect 数量"),
		new TagInsertItem("{xperfect:epp}", "XPerfect / Early PurePerfect 数量"),
		new TagInsertItem("{xperfect:lpp}", "XPerfect / Late PurePerfect 数量"),
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
		new TagInsertItem("{acc}", "准确率，最多 2 位小数"),
		new TagInsertItem("{acc:2}", "准确率，最多 2 位小数"),
		new TagInsertItem("{xacc}", "X-Accuracy，最多 2 位小数"),
		new TagInsertItem("{xacc:2}", "X-Accuracy，最多 2 位小数"),
		new TagInsertItem("{progress}", "地图进度，最多 2 位小数"),
		new TagInsertItem("{progress:2}", "地图进度，最多 2 位小数"),
		new TagInsertItem("{expr:progress*2}", "表达式：地图进度 * 2"),
		new TagInsertItem("{expr:sqrt(acc)}", "表达式：sqrt(准确率)"),
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

	private unsafe static int TextFormatCallback(ImGuiInputTextCallbackData* data)
	{
		_lastTextFormatCursorPos = data->CursorPos;
		return 0;
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
		ImGui.SetNextWindowSize(new Vector2(640f, 430f), ImGuiCond.Appearing);
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
		OverlayerRegexDocument regexDocument = OverlayerRegexProcessor.GetDocument(text);
		string result = FormatPreviewTags(regexDocument != null ? regexDocument.Body : text);
		return OverlayerRegexProcessor.Apply(result, regexDocument, "OV preview");
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
			.Replace("{xperfect:xpp}", "10")
			.Replace("{xperfect:epp}", "2")
			.Replace("{xperfect:lpp}", "1")
			.Replace("{music}", "Artist - SongName")
			.Replace("{level}", "Level Author");

		result = Regex.Replace(result, @"\{fps(?:[:](\d+))?\}", match => FormatPreviewNumber(144.0, match, 0));
		result = Regex.Replace(result, @"\{bpm(?:[:](\d+))?\}", match => FormatPreviewNumber(123.45, match, 2));
		result = Regex.Replace(result, @"\{tbpm(?:[:](\d+))?\}", match => FormatPreviewNumber(185.18, match, 2));
		result = Regex.Replace(result, @"\{cbpm(?:[:](\d+))?\}", match => FormatPreviewNumber(246.9, match, 2));
		result = Regex.Replace(result, @"\{acc(?:[:](\d+))?\}", match => FormatPreviewNumber(98.5, match, 2));
		result = Regex.Replace(result, @"\{xacc(?:[:](\d+))?\}", match => FormatPreviewNumber(97.0, match, 2));
		result = Regex.Replace(result, @"\{progress(?:[:](\d+))?\}", match => FormatPreviewNumber(50.0, match, 2));
		result = ReplacePreviewExpressions(result);
		return result;
	}

	private static string ReplacePreviewExpressions(string text)
	{
		if (string.IsNullOrEmpty(text) || text.IndexOf("{expr:", StringComparison.OrdinalIgnoreCase) < 0)
		{
			return text;
		}

		StringBuilder builder = new StringBuilder(text.Length);
		int index = 0;
		while (index < text.Length)
		{
			int open = text.IndexOf("{expr:", index, StringComparison.OrdinalIgnoreCase);
			if (open < 0)
			{
				builder.Append(text, index, text.Length - index);
				break;
			}

			builder.Append(text, index, open - index);
			int close = FindPreviewExpressionClose(text, open);
			if (close < 0)
			{
				builder.Append(text, open, text.Length - open);
				break;
			}

			string expression = text.Substring(open + 6, close - open - 6);
			builder.Append(FormatPreviewExpression(expression));
			index = close + 1;
		}

		return builder.ToString();
	}

	private static int FindPreviewExpressionClose(string text, int open)
	{
		int depth = 0;
		for (int i = open + 6; i < text.Length; i++)
		{
			char c = text[i];
			if (c == '{')
			{
				depth++;
			}
			else if (c == '}')
			{
				if (depth == 0)
				{
					return i;
				}
				depth--;
			}
		}

		return -1;
	}

	private static string FormatPreviewExpression(string expression)
	{
		string normalized = Regex.Replace(expression ?? string.Empty, @"\{([^{}]+)\}", match =>
		{
			string name = match.Groups[1].Value.Trim();
			int colon = name.IndexOf(':');
			if (colon > 0 && int.TryParse(name.Substring(colon + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
			{
				name = name.Substring(0, colon);
			}
			return name;
		});

		if (!TryEvaluatePreviewExpression(normalized, out double value))
		{
			return "0";
		}

		return FormatPreviewTrimZeros(value, 2);
	}

	private static string FormatPreviewTrimZeros(double value, int decimals)
	{
		decimals = Math.Max(0, Math.Min(6, decimals));
		if (double.IsNaN(value) || double.IsInfinity(value))
		{
			value = 0.0;
		}

		double rounded = Math.Round(value, decimals, MidpointRounding.AwayFromZero);
		if (decimals == 0)
		{
			return rounded.ToString("0", CultureInfo.InvariantCulture);
		}

		return rounded.ToString("0." + new string('#', decimals), CultureInfo.InvariantCulture);
	}

	private static bool TryEvaluatePreviewExpression(string expression, out double value)
	{
		value = 0.0;
		try
		{
			var parser = new PreviewExpressionParser(expression, ResolvePreviewExpressionVariable);
			return parser.TryEvaluate(out value);
		}
		catch
		{
			value = 0.0;
			return false;
		}
	}

	private static double ResolvePreviewExpressionVariable(string name)
	{
		switch ((name ?? string.Empty).Trim().ToLowerInvariant())
		{
			case "fps": return 144.0;
			case "kps": return (Object)KeyViewerManager.Instance != (Object)null ? KeyViewerManager.Instance.CurrentKPS : 7.0;
			case "tot": return Main.Settings != null ? Main.Settings.TotalHits : 128.0;
			case "ttile": return 128.0;
			case "atile": return 65.0;
			case "x": return 1.0;
			case "cur": return 2.0;
			case "bpm": return 123.45;
			case "tbpm": return 185.18;
			case "cbpm": return 246.9;
			case "acc": return 98.5;
			case "xacc": return 97.0;
			case "progress": return 50.0;
			case "maptime": return 150.0;
			case "maptime:p":
			case "maptimep": return 45.0;
			case "musictime": return 180.0;
			case "musictime:p":
			case "musictimep": return 60.0;
			case "te": return 1.0;
			case "ve": return 2.0;
			case "ep": return 3.0;
			case "p": return 45.0;
			case "lp": return 3.0;
			case "vl": return 2.0;
			case "tl": return 1.0;
			case "miss": return 0.0;
			case "fm": return 0.0;
			case "fo": return 0.0;
			case "combo": return 34.0;
			case "combo:p":
			case "combop": return 12.0;
			case "xperfect:xpp":
			case "xperfectxpp": return 10.0;
			case "xperfect:epp":
			case "xperfectepp": return 2.0;
			case "xperfect:lpp":
			case "xperfectlpp": return 1.0;
			default: return 0.0;
		}
	}

	private sealed class PreviewExpressionParser
	{
		private readonly string _text;
		private readonly Func<string, double> _resolveVariable;
		private int _index;

		public PreviewExpressionParser(string text, Func<string, double> resolveVariable)
		{
			_text = text ?? string.Empty;
			_resolveVariable = resolveVariable;
		}

		public bool TryEvaluate(out double value)
		{
			value = ParseAddSubtract();
			SkipWhitespace();
			if (_index < _text.Length || double.IsNaN(value) || double.IsInfinity(value))
			{
				value = 0.0;
				return false;
			}
			return true;
		}

		private double ParseAddSubtract()
		{
			double value = ParseMultiplyDivide();
			while (true)
			{
				SkipWhitespace();
				if (Match('+')) value += ParseMultiplyDivide();
				else if (Match('-')) value -= ParseMultiplyDivide();
				else return value;
			}
		}

		private double ParseMultiplyDivide()
		{
			double value = ParsePower();
			while (true)
			{
				SkipWhitespace();
				if (Match('*')) value *= ParsePower();
				else if (Match('/'))
				{
					double denominator = ParsePower();
					value = Math.Abs(denominator) <= double.Epsilon ? 0.0 : value / denominator;
				}
				else return value;
			}
		}

		private double ParsePower()
		{
			double value = ParseUnary();
			SkipWhitespace();
			if (Match('^'))
			{
				value = Math.Pow(value, ParsePower());
			}
			return value;
		}

		private double ParseUnary()
		{
			SkipWhitespace();
			if (Match('+')) return ParseUnary();
			if (Match('-')) return -ParseUnary();
			return ParsePrimary();
		}

		private double ParsePrimary()
		{
			SkipWhitespace();
			if (Match('('))
			{
				double value = ParseAddSubtract();
				if (!Match(')')) throw new FormatException();
				return value;
			}

			if (_index < _text.Length && (char.IsDigit(_text[_index]) || _text[_index] == '.'))
			{
				return ParseNumber();
			}

			string identifier = ParseIdentifier();
			if (identifier.Length == 0) throw new FormatException();
			if (string.Equals(identifier, "sqrt", StringComparison.OrdinalIgnoreCase))
			{
				if (!Match('(')) throw new FormatException();
				double value = ParseAddSubtract();
				if (!Match(')')) throw new FormatException();
				return Math.Sqrt(Math.Max(0.0, value));
			}

			return _resolveVariable != null ? _resolveVariable(identifier) : 0.0;
		}

		private double ParseNumber()
		{
			int start = _index;
			bool hasDot = false;
			while (_index < _text.Length)
			{
				char c = _text[_index];
				if (char.IsDigit(c)) _index++;
				else if (c == '.' && !hasDot)
				{
					hasDot = true;
					_index++;
				}
				else break;
			}

			if (!double.TryParse(_text.Substring(start, _index - start), NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
			{
				throw new FormatException();
			}
			return value;
		}

		private string ParseIdentifier()
		{
			int start = _index;
			while (_index < _text.Length)
			{
				char c = _text[_index];
				if (char.IsLetterOrDigit(c) || c == '_' || c == ':' || c == '.')
				{
					_index++;
				}
				else break;
			}
			return _text.Substring(start, _index - start);
		}

		private bool Match(char expected)
		{
			SkipWhitespace();
			if (_index < _text.Length && _text[_index] == expected)
			{
				_index++;
				return true;
			}
			return false;
		}

		private void SkipWhitespace()
		{
			while (_index < _text.Length && char.IsWhiteSpace(_text[_index]))
			{
				_index++;
			}
		}
	}

	private static void ReloadSettingsAfterImport(string sourcePath)
	{
		Main.Settings = UnityModManager.ModSettings.Load<Settings>(Main.ModEntry);
		Main.Settings.InitNulls();
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

	private void ExportKeyViewerPackage()
	{
		try
		{
			string path = ModernFileDialog.ShowSaveFileDialog(
				"导出 KeyViewer 配置",
				"CheryTools KeyViewer 配置 (*.ctkv)|*.ctkv",
				CheryToolsAssets.GameRoot,
				"CheryTools_KeyViewer.ctkv"
			);
			
			if (!string.IsNullOrEmpty(path))
			{
				path = CheryToolsAssets.ExportKeyViewerPackage(Main.Settings, path);
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
				_selectedOvSidebarTab = 0;
				_selectedOvSidebarImgTab = 0;
				_selectedOvSidebarVideoTab = 0;
				_selectedOvSidebarBarTab = 0;
				TextureManager.Clear();
				VideoTextureManager.Shutdown();
				SdfTextRenderer.Shutdown();
				ImGuiController.NeedsFontAtlasRebuild = true;
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

	private List<RichTextParser.ParsedSegment> _editingSegList;

	private int _editingSegIndex = -1;

	private int _editingBlockIndex = -1;

	private System.Numerics.Vector4 _editingColor;

	private bool _editingColorDirty = false;

	public static bool ShowToolsWindow = true;

	public static bool ShowKeyviewerWindow = false;

	public static bool ShowOverlayerWindow = false;

	private static bool ShowOverlayerTextEditorWindow = false;

	public static bool ShowSettingsWindow = false;

	private static bool ShowHelpWindow = false;

	private int _currentToolTab;

	private bool _waitingForToggleMenuKey = false;

	private bool _waitingForToolsLimitedKey = false;

	private bool _editingImGuiPanelScale = false;

	private float _pendingImGuiPanelScale = 1.0f;

	private bool _editingOverlayUpdateRate = false;

	private float _pendingOverlayUpdateRate = 240.0f;

	private bool _editingImageRenderScale = false;

	private float _pendingImageRenderScale = 1.0f;

	private int _windowResetTargetIndex = 0;

	private static bool _centerToolsWindowNextFrame = false;

	private static bool _centerKeyviewerWindowNextFrame = false;

	private static bool _centerOverlayerWindowNextFrame = false;

	private static bool _centerSettingsWindowNextFrame = false;

	private static bool _centerHelpWindowNextFrame = false;

	private string _gameUIDeveloperKeyInput = "";

	private bool _gameUIDeveloperKeyFailed = false;

	private string _legacyKeyViewerImportMessage = "";

	private string _keyViewerExportMessage = "";

	private string _overlayerExportMessage = "";

	private string _languageConfigMessage = "";

	private string _cloudSyncStatusMessage = "";
	private bool _cloudSyncStatusIsError = false;

	

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

	private bool DrawAxisGradientSettings(string label, string id, KVAxisGradient gradient)
	{
		if (gradient == null) return false;
		bool changed = false;
		ImGui.Text(label);
		ImGui.Indent();
		bool verticalEnabled = gradient.VerticalEnabled;
		if (ImGui.Checkbox(Tr("kv.gradientVertical", "纵向渐变") + "##" + id + "_v_enable", ref verticalEnabled))
		{
			gradient.VerticalEnabled = verticalEnabled;
			changed = true;
		}
		if (verticalEnabled)
		{
			if (DrawColorPicker(Tr("kv.gradientVerticalEnd", "底部颜色") + "##" + id + "_v_color", ref gradient.VerticalEndColor))
			{
				changed = true;
			}
		}
		bool horizontalEnabled = gradient.HorizontalEnabled;
		if (ImGui.Checkbox(Tr("kv.gradientHorizontal", "横向渐变") + "##" + id + "_h_enable", ref horizontalEnabled))
		{
			gradient.HorizontalEnabled = horizontalEnabled;
			changed = true;
		}
		if (horizontalEnabled)
		{
			if (DrawColorPicker(Tr("kv.gradientHorizontalEnd", "右侧颜色") + "##" + id + "_h_color", ref gradient.HorizontalEndColor))
			{
				changed = true;
			}
		}
		ImGui.Unindent();
		return changed;
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

		ImGui.BeginChild("KVConfigSidebar", new Vector2(150f, 0f), ImGuiChildFlags.Borders);
		if (ImGui.Button(Tr("kv.newConfig", "新建配置"), new Vector2(-1f, 0f)))
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

	private void DrawWindowResetSettings()
	{
		List<string> labels = new List<string>();
		List<Action> actions = new List<Action>();

		if (ShowToolsWindow)
		{
			labels.Add(Tr("window.tools", "Tools"));
			actions.Add(() => _centerToolsWindowNextFrame = true);
		}
		if (ShowKeyviewerWindow)
		{
			labels.Add(Tr("window.keyViewer", "KeyViewer"));
			actions.Add(() => _centerKeyviewerWindowNextFrame = true);
		}
		if (FreeMakeEditor.IsOpen)
		{
			labels.Add(Tr("window.kvEditor", "KV 编辑器"));
			actions.Add(FreeMakeEditor.RequestCenterOnScreen);
		}
		if (ShowOverlayerWindow)
		{
			labels.Add(Tr("window.overlayer", "Overlayer"));
			actions.Add(() => _centerOverlayerWindowNextFrame = true);
		}
		if (ShowHelpWindow)
		{
			labels.Add(Tr("window.help", "帮助"));
			actions.Add(() => _centerHelpWindowNextFrame = true);
		}
		if (ShowSettingsWindow)
		{
			labels.Add(Tr("window.settings", "设置"));
			actions.Add(() => _centerSettingsWindowNextFrame = true);
		}

		ImGui.Text(Tr("settings.windowPosition", "窗口位置"));
		if (labels.Count == 0)
		{
			ImGui.TextColored(new Vector4(0.65f, 0.65f, 0.65f, 1f), Tr("settings.noOpenWindows", "当前没有可重置的已打开窗口"));
			return;
		}

		if (_windowResetTargetIndex < 0 || _windowResetTargetIndex >= labels.Count)
		{
			_windowResetTargetIndex = 0;
		}

		ImGui.SetNextItemWidth(180f);
		if (ImGui.BeginCombo(Tr("settings.resetWindow", "重置窗口") + "##WindowResetTarget", labels[_windowResetTargetIndex]))
		{
			for (int i = 0; i < labels.Count; i++)
			{
				bool selected = i == _windowResetTargetIndex;
				if (ImGui.Selectable(labels[i], selected))
				{
					_windowResetTargetIndex = i;
				}
				if (selected)
				{
					ImGui.SetItemDefaultFocus();
				}
			}
			ImGui.EndCombo();
		}
		ImGui.SameLine();
		if (ImGui.Button(Tr("settings.resetToCenter", "重置到屏幕中央") + "##ResetWindowToCenter"))
		{
			actions[_windowResetTargetIndex].Invoke();
		}
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

	private void DrawUpdateSettings()
	{
		ImGui.Text(Tr("settings.update", "更新"));
		ImGui.Text(string.Format(Tr("settings.currentVersion", "当前版本: {0}"), GithubUpdateManager.CurrentVersion));

		bool busy = GithubUpdateManager.IsBusy;
		if (busy)
		{
			ImGui.BeginDisabled();
		}
		if (ImGui.Button(Tr("settings.checkUpdate", "检查更新") + "##CheckGithubUpdate"))
		{
			GithubUpdateManager.CheckForUpdates();
		}
		if (busy)
		{
			ImGui.EndDisabled();
		}

		if (GithubUpdateManager.UpdateAvailable)
		{
			ImGui.TextColored(new Vector4(0.3f, 1f, 0.45f, 1f), string.Format(Tr("settings.updateAvailable", "发现新版本: {0}"), GithubUpdateManager.LatestVersion));
			string assetName = GithubUpdateManager.AssetName;
			if (!string.IsNullOrEmpty(assetName))
			{
				ImGui.TextWrapped(string.Format(Tr("settings.updatePackage", "更新包: {0}"), assetName));
			}

			string notes = GithubUpdateManager.ReleaseNotes;
			if (!string.IsNullOrWhiteSpace(notes) && ImGui.CollapsingHeader(Tr("settings.updateNotes", "更新说明") + "##GithubUpdateNotes"))
			{
				ImGui.TextWrapped(notes.Length > 1200 ? notes.Substring(0, 1200) + "..." : notes);
			}

			if (busy)
			{
				ImGui.BeginDisabled();
			}
			if (ImGui.Button(Tr("settings.downloadUpdate", "下载更新包") + "##DownloadGithubUpdate"))
			{
				GithubUpdateManager.DownloadUpdate();
			}
			if (busy)
			{
				ImGui.EndDisabled();
			}

			ImGui.SameLine();
			bool canInstall = GithubUpdateManager.DownloadReady && !busy;
			if (!canInstall)
			{
				ImGui.BeginDisabled();
			}
			if (ImGui.Button(Tr("settings.installAndRestart", "安装并重启") + "##InstallGithubUpdate"))
			{
				GithubUpdateManager.InstallAndRestart();
			}
			if (!canInstall)
			{
				ImGui.EndDisabled();
			}

			if (GithubUpdateManager.DownloadReady)
			{
				ImGui.TextWrapped(string.Format(Tr("settings.updateDownloaded", "已下载: {0}"), GithubUpdateManager.DownloadedZipPath));
			}
		}

		string status = GithubUpdateManager.Status;
		if (!string.IsNullOrEmpty(status))
		{
			ImGui.TextWrapped(status);
		}
	}

	private void DrawCloudSyncSection()
	{
		ImGui.Text(Tr("settings.cloudSync", "Steam 云端同步"));

		if (!CloudSettingsManager.IsSteamAvailable)
		{
			ImGui.TextColored(new Vector4(1f, 0.6f, 0.3f, 1f), Tr("settings.cloudSync.steamUnavailable", "Steam 不可用，云端同步功能已禁用。请通过 Steam 启动游戏。"));
			return;
		}

		if (ImGui.Button(Tr("settings.cloudSync.upload", "同步 Mod 设置至 Steam 云端"), new Vector2(220f, 0f)))
		{
			if (Main.Settings.UploadToCloud(Main.ModEntry))
			{
				_cloudSyncStatusMessage = Tr("settings.cloudSync.uploadSuccess", "Mod 设置已同步至 Steam 云端。");
				_cloudSyncStatusIsError = false;
			}
			else
			{
				_cloudSyncStatusMessage = Tr("settings.cloudSync.uploadFailed", "同步失败，请查看日志。");
				_cloudSyncStatusIsError = true;
			}
		}

		ImGui.SameLine();
		if (ImGui.Button(Tr("settings.cloudSync.download", "从 Steam 云端同步设置并应用"), new Vector2(220f, 0f)))
		{
			if (!CloudSettingsManager.HasCloudFile())
			{
				_cloudSyncStatusMessage = Tr("settings.cloudSync.noCloudFile", "云端暂无 Mod 设置。");
				_cloudSyncStatusIsError = true;
			}
			else if (Main.Settings.DownloadFromCloud(Main.ModEntry))
			{
				_cloudSyncStatusMessage = Tr("settings.cloudSync.downloadSuccess", "已从 Steam 云端同步设置并应用。");
				_cloudSyncStatusIsError = false;

				if ((Object)KeyViewerManager.Instance != (Object)null)
				{
					KeyViewerManager.Instance.RefreshKeys();
				}
				InputInterceptor.UpdateAllowedKeys();
				VideoTextureManager.Shutdown();
				ImGuiController.NeedsFontAtlasRebuild = true;
			}
			else
			{
				_cloudSyncStatusMessage = Tr("settings.cloudSync.downloadFailed", "同步失败，请查看日志。");
				_cloudSyncStatusIsError = true;
			}
		}

		if (!string.IsNullOrEmpty(_cloudSyncStatusMessage))
		{
			ImGui.PushStyleColor(ImGuiCol.Text, _cloudSyncStatusIsError
				? new Vector4(1f, 0.35f, 0.35f, 1f)
				: new Vector4(0.3f, 1f, 0.45f, 1f));
			ImGui.TextWrapped(_cloudSyncStatusMessage);
			ImGui.PopStyleColor();
		}
	}

	private void DrawToolsInputLimitSettings()
	{
		if (Main.Settings.ToolsLimitedKeys == null)
		{
			Main.Settings.ToolsLimitedKeys = new List<KeyCode>();
		}
		if (Main.Settings.ToolsLimitedKeys.Count > 30)
		{
			Main.Settings.ToolsLimitedKeys.RemoveRange(30, Main.Settings.ToolsLimitedKeys.Count - 30);
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
		DrawInlineHelpText(Tr("tools.inputLimit.help", "启用后只放行下方登记的按键，最多 30 个；菜单热键、Esc、Ctrl、F10 保底放行。"));

		string addButtonLabel = _waitingForToolsLimitedKey ? Tr("tools.inputLimit.waiting", "[等待按键...]") : Tr("tools.inputLimit.add", "添加限制键");
		if (ImGui.Button(addButtonLabel, new Vector2(140f, 0f)))
		{
			_waitingForToolsLimitedKey = true;
		}
		ImGui.SameLine();
		ImGui.Text($"{Main.Settings.ToolsLimitedKeys.Count}/30");
		if (_waitingForToolsLimitedKey)
		{
			foreach (KeyCode value in SettingsHotkeyCandidates)
			{
				if (!Input.GetKeyDown(value))
				{
					continue;
				}

				_waitingForToolsLimitedKey = false;
				if (Main.Settings.ToolsLimitedKeys.Count < 30 && !Main.Settings.ToolsLimitedKeys.Contains(value))
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
		CenterNextWindowIfRequested(ref _centerHelpWindowNextFrame, new Vector2(680f, 560f));
		ImGui.SetNextWindowSize(new Vector2(680f, 560f), ImGuiCond.FirstUseEver);
		if (!ImGui.Begin(Tr("window.help", "帮助"), ref ShowHelpWindow))
		{
			ImGui.End();
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
			DrawHelpBullet("help.quickStart.2", "Settings 中可以切换语言、调整 ImGui 面板缩放、导入导出总配置，并重置窗口位置。");
			DrawHelpBullet("help.quickStart.3", "KV 与 OV 都支持独立导入导出分享包，外置字体、图片、视频会一起打包。");
			DrawHelpBullet("help.quickStart.4", "大多数修改会立即生效；完成配置后可在设置中导出 .cyt，或在 KV/OV 面板导出 .ctkv/.ctov。");
		}

		if (ImGui.CollapsingHeader(Tr("help.general", "通用操作"), ImGuiTreeNodeFlags.DefaultOpen))
		{
			DrawHelpBullet("help.general.1", "鼠标悬停在输入框或滑块上时，可以直接滚轮或拖动调整数值。");
			DrawHelpBullet("help.general.2", "按住 Ctrl 点击滑块或拖动条，可以切换为手动输入数值。");
			DrawHelpBullet("help.general.3", "文本输入后按 Enter 或点击其它位置即可提交。");
			DrawHelpBullet("help.general.4", "颜色方块可以打开颜色编辑器，Alpha 控制透明度。");
			DrawHelpBullet("help.general.5", "如果窗口被拖出屏幕，可以在设置中选择对应窗口并重置到屏幕中央。");
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
			DrawHelpBullet("help.ov.2", "文本模块支持 Tag、富文本颜色/字号、描边、阴影、语法高亮编辑器和右侧实时预览。");
			DrawHelpBullet("help.ov.3", "Tag 插入窗口支持搜索；例如 {fps}、{progress:2}、{bpm:2}、{acc:2}、{wtime}。");
			DrawHelpBullet("help.ov.4", "数字 Tag 支持 :位数 控制小数显示，并会自动去掉无意义的末尾 0。");
			DrawHelpBullet("help.ov.5", "文本编辑器支持 #import regex 和 #regex /pattern/replacement/，用于对最终文本做正则替换。");
			DrawHelpBullet("help.ov.6", "图片和视频模块可以设置位置、尺寸、旋转、锚点、透明度和对齐；OV 视频最多两个，仅支持 .mp4。");
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
			DrawSponsorEntry("@RWspawn", "https://space.bilibili.com/535013673", 1);
			DrawSponsorEntry("@Leaked-翼龙", "https://space.bilibili.com/616045770", 2);
			DrawSponsorEntry("@Sam1nA", "https://space.bilibili.com/512479134", 3);
			DrawSponsorEntry("@346丶pomegranate", "https://space.bilibili.com/561924970", 4);
			DrawSponsorEntry("@濑户星仔_洛辰", "https://space.bilibili.com/1942218610", 5);
			DrawSponsorEntry("@杰西杰西杰-_-", "https://space.bilibili.com/3546385762749238", 6);
			DrawSponsorEntry("@BaiZhu_L", "https://space.bilibili.com/1278798931", 7);
			ImGui.Separator();
			ImGui.TextUnformatted("QQ");
			DrawSponsorEntry("@罗氓免 296**3", null, 8);
		}

		ImGui.End();
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

		bool enabled = config.IsEnabled;
		if (ImGui.Checkbox(Tr("kv.configEnable", "启用显示") + "##kv_config_enabled", ref enabled))
		{
			config.IsEnabled = enabled;
			InputInterceptor.UpdateAllowedKeys();
			Main.RequestSave();
		}
		ImGui.SameLine();
		bool showInGame = config.ShowInGame;
		if (ImGui.Checkbox(Tr("kv.showInGame", "游戏中显示") + "##kv_config_show_in_game", ref showInGame))
		{
			config.ShowInGame = showInGame;
			Main.RequestSave();
		}

		int nodeCount = config.Nodes != null ? config.Nodes.Count : 0;
		ImGui.Text(string.Format(Tr("kv.nodeCount", "节点数量: {0}"), nodeCount));

		if (ImGui.Button(Tr("kv.openEditor", "打开 FreeMake 编辑器"), new Vector2(200f, 30f)))
		{
			FreeMakeEditor.IsOpen = true;
		}
		ImGui.SameLine();
		if (ImGui.Button(Tr("kv.clearNodes", "清空节点"), new Vector2(90f, 30f)))
		{
			if (config.Nodes != null) config.Nodes.Clear();
			InputInterceptor.UpdateAllowedKeys();
			Main.RequestSave();
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

		ImGui.Text(Tr("kv.replaceWithPreset", "用预设覆盖当前配置:"));
		if (ImGui.Button("16K##kv_cfg_reset_16")) ReplaceSelectedKeyViewerConfig(config, 16);
		ImGui.SameLine();
		if (ImGui.Button("12K##kv_cfg_reset_12")) ReplaceSelectedKeyViewerConfig(config, 12);
		ImGui.SameLine();
		if (ImGui.Button("10K##kv_cfg_reset_10")) ReplaceSelectedKeyViewerConfig(config, 10);
		ImGui.SameLine();
		if (ImGui.Button("8K##kv_cfg_reset_8")) ReplaceSelectedKeyViewerConfig(config, 8);
		ImGui.SameLine();
		if (ImGui.Button(Tr("kv.emptyPreset", "空白") + "##kv_cfg_reset_empty")) ReplaceSelectedKeyViewerConfig(config, 0);
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
		float textOffsetX = config.GlobalTextOffsetX;
		if (ImGui.DragFloat(Tr("kv.keyTextOffsetX", "配置按键文字X偏移"), ref textOffsetX, 1f))
		{
			config.GlobalTextOffsetX = textOffsetX;
			Main.RequestSave();
		}
		float textOffsetY = config.GlobalTextOffsetY;
		if (ImGui.DragFloat(Tr("kv.keyTextOffsetY", "配置按键文字Y偏移"), ref textOffsetY, 1f))
		{
			config.GlobalTextOffsetY = textOffsetY;
			Main.RequestSave();
		}
		float countOffsetX = config.GlobalCountOffsetX;
		if (ImGui.DragFloat(Tr("kv.countTextOffsetX", "配置计数文字X偏移"), ref countOffsetX, 1f))
		{
			config.GlobalCountOffsetX = countOffsetX;
			Main.RequestSave();
		}
		float countOffsetY = config.GlobalCountOffsetY;
		if (ImGui.DragFloat(Tr("kv.countTextOffsetY", "配置计数文字Y偏移"), ref countOffsetY, 1f))
		{
			config.GlobalCountOffsetY = countOffsetY;
			Main.RequestSave();
		}
		if (ImGui.Button(Tr("kv.reloadFont", "重新加载字体")))
		{
			ImportResourcePath(ref config.FontPath, "Fonts", true);
			ImGuiController.NeedsFontAtlasRebuild = true;
		}
		float scale = config.Scale;
		if (ImGui.SliderFloat(Tr("kv.scale", "缩放大小"), ref scale, 0.5f, 3f))
		{
			config.Scale = scale;
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
		ImGui.Spacing();
		ImGui.Text(Tr("kv.bulkSize", "批量修改当前配置按键大小"));
		bool sizeChanged = false;
		float defaultWidth = config.DefaultWidth;
		if (ImGui.DragFloat(Tr("kv.bulkWidth", "配置宽度设定") + "##bulk_w", ref defaultWidth, 1f, 10f, 500f))
		{
			config.DefaultWidth = defaultWidth;
			sizeChanged = true;
		}
		float defaultHeight = config.DefaultHeight;
		if (ImGui.DragFloat(Tr("kv.bulkHeight", "配置高度设定") + "##bulk_h", ref defaultHeight, 1f, 10f, 500f))
		{
			config.DefaultHeight = defaultHeight;
			sizeChanged = true;
		}
		if (sizeChanged)
		{
			Main.RequestSave();
		}
		if (ImGui.Button(Tr("kv.applySizeToAllKeys", "应用大小到当前配置的所有按键")))
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
		if (DrawAxisGradientSettings(Tr("kv.backgroundNormalGradient", "未按下背景渐变"), "kv_bg_norm_grad", config.BackgroundGradientNormal)) Main.RequestSave();
		if (DrawAxisGradientSettings(Tr("kv.backgroundPressedGradient", "触发背景渐变"), "kv_bg_press_grad", config.BackgroundGradientPressed)) Main.RequestSave();
		ImGui.Text(Tr("kv.border", "边框"));
		if (DrawColorPicker(Tr("kv.normal", "未按下") + "##border_norm", ref config.ColorBorderNormal)) Main.RequestSave();
		ImGui.SameLine();
		if (DrawColorPicker(Tr("kv.pressed", "触发") + "##border_press", ref config.ColorBorderPressed)) Main.RequestSave();
		if (DrawAxisGradientSettings(Tr("kv.borderNormalGradient", "未按下边框渐变"), "kv_border_norm_grad", config.BorderGradientNormal)) Main.RequestSave();
		if (DrawAxisGradientSettings(Tr("kv.borderPressedGradient", "触发边框渐变"), "kv_border_press_grad", config.BorderGradientPressed)) Main.RequestSave();
		ImGui.Text(Tr("kv.text", "文本"));
		if (DrawColorPicker(Tr("kv.normal", "未按下") + "##txt_norm", ref config.ColorTextNormal)) Main.RequestSave();
		ImGui.SameLine();
		if (DrawColorPicker(Tr("kv.pressed", "触发") + "##txt_press", ref config.ColorTextPressed)) Main.RequestSave();
		if (DrawAxisGradientSettings(Tr("kv.textNormalGradient", "未按下文字渐变"), "kv_text_norm_grad", config.TextGradientNormal)) Main.RequestSave();
		if (DrawAxisGradientSettings(Tr("kv.textPressedGradient", "触发文字渐变"), "kv_text_press_grad", config.TextGradientPressed)) Main.RequestSave();
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
			if (ImGui.BeginPopup("kv_keypress_anim_easing_popup"))
			{
				string[] easingNames = new string[] { "linear", "ease-out-quad", "ease-in-out-quad", "ease-out-cubic", "ease-out-back", "ease-in-out-sine" };
				for (int i = 0; i < easingNames.Length; i++)
				{
					string easingName = easingNames[i];
					if (ImGui.Selectable(easingName, string.Equals(config.KeyPressAnimationEasing, easingName, StringComparison.OrdinalIgnoreCase)))
					{
						config.KeyPressAnimationEasing = easingName;
						Main.RequestSave();
					}
				}
				ImGui.EndPopup();
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

	private void DrawOverlayerTextPreview(OverlayerText overlayerText, int textIndex, string previewId, Vector2 size)
	{
		if (overlayerText == null)
		{
			return;
		}

		ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0.1f, 0.1f, 0.1f, 0.5f));
		try
		{
			bool isChildVisible = ImGui.BeginChild(previewId, size, ImGuiChildFlags.Borders);
			try
			{
				if (!isChildVisible)
				{
					return;
				}

				OverlayerRegexDocument regexDocument = OverlayerRegexProcessor.GetDocument(overlayerText.TextFormat);
				bool canEditPreviewSegments = regexDocument == null || !regexDocument.RegexEnabled;
				string previewSource = canEditPreviewSegments
					? (overlayerText.TextFormat ?? string.Empty)
					: FormatPreviewText(overlayerText.TextFormat);
				if (!canEditPreviewSegments)
				{
					ImGui.TextColored(new Vector4(0.95f, 0.68f, 0.30f, 1f), "正则已启用：预览显示最终结果，点击上色已关闭。");
				}
				List<RichTextParser.ParsedSegment> list = RichTextParser.Parse(previewSource, new Vector4(overlayerText.TextColor[0], overlayerText.TextColor[1], overlayerText.TextColor[2], overlayerText.TextColor[3]));
				bool newLine = true;
				for (int i = 0; i < list.Count; i++)
				{
					RichTextParser.ParsedSegment segment = list[i];
					string[] lines = segment.RenderText.Split('\n');
					for (int line = 0; line < lines.Length; line++)
					{
						if (line > 0)
						{
							newLine = true;
						}
						if (!newLine)
						{
							ImGui.SameLine(0f, 0f);
						}

						string previewText = canEditPreviewSegments ? FormatPreviewTags(lines[line]) : lines[line];
						if (segment.HasSizeTag && segment.SizeValue > 0)
						{
							ImGui.SetWindowFontScale(segment.SizeValue / 48f * ((overlayerText.FontSize > 0) ? overlayerText.FontSize : 100f) / ImGui.GetFontSize());
						}
						else if (segment.HasSizeTag && segment.SizeValue < 0)
						{
							ImGui.SetWindowFontScale(-segment.SizeValue);
						}

						ImGui.TextColored(segment.Color, previewText);
						if (segment.HasSizeTag)
						{
							ImGui.SetWindowFontScale(1.0f);
						}
						if (canEditPreviewSegments && ImGui.IsItemHovered())
						{
							ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
						}
						if (canEditPreviewSegments && ImGui.IsItemClicked())
						{
							_editingBlockIndex = textIndex;
							_editingSegIndex = i;
							_editingSegList = list;
							_editingColor = segment.Color;
							_editingColorDirty = false;
							ImGui.OpenPopup("ColorEditPopup");
						}
						newLine = false;
					}
				}

				if (ImGui.BeginPopup("ColorEditPopup"))
				{
					ImGui.Text("修改该片段文字颜色");
					if (ImGui.ColorPicker4("##picker", ref _editingColor))
					{
						_editingColorDirty = true;
					}
					if (_editingColorDirty && ImGui.IsItemDeactivatedAfterEdit() && _editingSegList != null && _editingSegIndex >= 0 && _editingSegIndex < _editingSegList.Count)
					{
						RichTextParser.ParsedSegment segment = _editingSegList[_editingSegIndex];
						if (ApplyColorToTextSegment(overlayerText, segment, _editingColor))
						{
							_editingColorDirty = false;
							_editingSegList = null;
							_editingSegIndex = -1;
							Main.RequestSave();
							ImGui.CloseCurrentPopup();
						}
					}
					if (ImGui.Button("恢复默认颜色", new Vector2(-1f, 0f)) && _editingSegList != null && _editingSegIndex >= 0 && _editingSegIndex < _editingSegList.Count)
					{
						RichTextParser.ParsedSegment segment = _editingSegList[_editingSegIndex];
						if (RemoveColorFromTextSegment(overlayerText, segment))
						{
							_editingColorDirty = false;
							_editingSegList = null;
							_editingSegIndex = -1;
							Main.RequestSave();
						}
						ImGui.CloseCurrentPopup();
					}
					ImGui.EndPopup();
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

	private bool ApplyColorToTextSegment(OverlayerText overlayerText, RichTextParser.ParsedSegment segment, Vector4 color)
	{
		if (overlayerText == null || string.IsNullOrEmpty(overlayerText.TextFormat) || string.IsNullOrEmpty(segment.RenderText))
		{
			return false;
		}

		string text = overlayerText.TextFormat;
		int start = Math.Max(0, Math.Min(segment.SourceStart, text.Length));
		int length = Math.Max(0, Math.Min(segment.SourceLength, text.Length - start));
		if (length <= 0)
		{
			return false;
		}

		string source = text.Substring(start, length);
		ExpandSegmentToOuterColorTag(text, ref start, ref length);
		source = text.Substring(start, length);
		string wrapped = "<color=#" + ColorToHex(color) + ">" + StripOuterColorTag(source) + "</color>";
		if (source == wrapped)
		{
			return false;
		}
		overlayerText.TextFormat = text.Substring(0, start) + wrapped + text.Substring(start + length);
		return true;
	}

	private bool RemoveColorFromTextSegment(OverlayerText overlayerText, RichTextParser.ParsedSegment segment)
	{
		if (overlayerText == null || string.IsNullOrEmpty(overlayerText.TextFormat))
		{
			return false;
		}

		string text = overlayerText.TextFormat;
		int start = Math.Max(0, Math.Min(segment.SourceStart, text.Length));
		int length = Math.Max(0, Math.Min(segment.SourceLength, text.Length - start));
		if (length <= 0)
		{
			return false;
		}

		string source = text.Substring(start, length);
		ExpandSegmentToOuterColorTag(text, ref start, ref length);
		source = text.Substring(start, length);
		string stripped = StripOuterColorTag(source);
		if (stripped == source)
		{
			return false;
		}

		overlayerText.TextFormat = text.Substring(0, start) + stripped + text.Substring(start + length);
		return true;
	}

	private static string StripOuterColorTag(string source)
	{
		if (string.IsNullOrEmpty(source))
		{
			return source;
		}

		Match match = Regex.Match(source, @"^\s*<\s*color\s*=\s*#?[0-9a-fA-F]{6,8}\s*>([\s\S]*)<\s*/\s*color\s*>\s*$", RegexOptions.IgnoreCase);
		return match.Success ? match.Groups[1].Value : source;
	}

	private static void ExpandSegmentToOuterColorTag(string text, ref int start, ref int length)
	{
		if (string.IsNullOrEmpty(text) || length <= 0)
		{
			return;
		}

		int contentStart = Math.Max(0, Math.Min(start, text.Length));
		int contentEnd = Math.Max(contentStart, Math.Min(start + length, text.Length));
		Match opening = null;
		foreach (Match match in ColorTagBoundaryRegex.Matches(text))
		{
			if (match.Index >= contentStart)
			{
				break;
			}
			if (match.Groups[1].Success)
			{
				opening = null;
			}
			else
			{
				opening = match;
			}
		}

		if (opening == null)
		{
			return;
		}

		Match closing = null;
		foreach (Match match in ColorTagBoundaryRegex.Matches(text, contentEnd))
		{
			if (match.Groups[1].Success)
			{
				closing = match;
				break;
			}
			return;
		}

		if (closing == null)
		{
			return;
		}

		start = opening.Index;
		length = closing.Index + closing.Length - opening.Index;
	}

	private string BuildTextFormatFromSegments(List<RichTextParser.ParsedSegment> segments)
	{
		if (segments == null)
		{
			return string.Empty;
		}

		StringBuilder builder = new StringBuilder();
		foreach (RichTextParser.ParsedSegment segment in segments)
		{
			if (segment.HasSizeTag && segment.SizeValue < 0)
			{
				builder.Append("<size=" + (-segment.SizeValue * 100f).ToString("0.##", CultureInfo.InvariantCulture) + "%>");
			}
			else if (segment.HasSizeTag && segment.SizeValue > 0)
			{
				builder.Append("<size=" + segment.SizeValue.ToString("0.##", CultureInfo.InvariantCulture) + ">");
			}

			if (segment.HasColorTag)
			{
				builder.Append("<color=#" + ColorToHex(segment.Color) + ">" + segment.RenderText + "</color>");
			}
			else
			{
				builder.Append(segment.RenderText);
			}

			if (segment.HasSizeTag)
			{
				builder.Append("</size>");
			}
		}
		return builder.ToString();
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

		ImGui.TextColored(new Vector4(0.65f, 0.78f, 1f, 1f), "左侧编辑公式，右侧实时预览。");
		ImGui.SameLine();
		if (ImGui.Button("插入 Tag##TextEditorInsertTag"))
		{
			ImGui.OpenPopup("TagSelectorPopup");
		}
		DrawOverlayerTagInsertPopup(overlayerText);
		ImGui.Separator();

		Vector2 avail = ImGui.GetContentRegionAvail();
		float leftWidth = Math.Max(360f, avail.X * 0.56f);
		ImGui.BeginChild("OvTextEditorLeftPane", new Vector2(leftWidth, 0f), ImGuiChildFlags.Borders);
		try
		{
			ImGui.Text("公式编辑");
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

		ImGui.SameLine();
		ImGui.BeginChild("OvTextEditorRightPane", new Vector2(0f, 0f), ImGuiChildFlags.Borders);
		try
		{
			ImGui.Text("效果预览 (点击任意文字即可局部上色)");
			DrawOverlayerTextPreview(overlayerText, _overlayerTextEditorIndex, $"preview_ov_editor_{_overlayerTextEditorIndex}", new Vector2(0f, 0f));
		}
		finally
		{
			ImGui.EndChild();
		}

		ImGui.End();
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
		ImGui.PushStyleColor(ImGuiCol.MenuBarBg, new System.Numerics.Vector4(0.067f, 0.082f, 0.106f, 0.86f));
		ImGui.PushStyleColor(ImGuiCol.Separator, new System.Numerics.Vector4(1f, 1f, 1f, 0.14f));
		try
		{
			if (ImGui.BeginMainMenuBar())
			{
				try
				{
					ImGui.PushStyleColor(ImGuiCol.Text, new System.Numerics.Vector4(0.21f, 0.85f, 1f, 1f));
					ImGui.Text("CheryTools");
					ImGui.PopStyleColor();
					ImGui.Separator();
					if (DrawTopBarButton(Tr("top.tools", "Tools"), ShowToolsWindow))
					{
						ShowToolsWindow = !ShowToolsWindow;
					}
					ImGui.SameLine();
					if (DrawTopBarButton(Tr("top.keyViewer", "KeyViewer"), ShowKeyviewerWindow))
					{
						ShowKeyviewerWindow = !ShowKeyviewerWindow;
					}
					ImGui.SameLine();
					if (DrawTopBarButton(Tr("top.overlayer", "Overlayer"), ShowOverlayerWindow))
					{
						ShowOverlayerWindow = !ShowOverlayerWindow;
					}
					ImGui.SameLine();
					if (DrawTopBarButton(Tr("top.settings", "\u8BBE\u7F6E"), ShowSettingsWindow))
					{
						ShowSettingsWindow = !ShowSettingsWindow;
					}
					ImGui.SameLine();
					if (DrawTopBarButton(Tr("top.help", "\u5E2E\u52A9"), ShowHelpWindow))
					{
						ShowHelpWindow = !ShowHelpWindow;
					}
					bool drawLegacyTopBarItems = false;
					if (drawLegacyTopBarItems)
					{
					if (ImGui.MenuItem("设置"))
					{
						ShowSettingsWindow = !ShowSettingsWindow;
					}
					if (ImGui.MenuItem("帮助"))
					{
						ShowHelpWindow = !ShowHelpWindow;
					}
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
			ImGui.PopStyleColor(2);
		}
		if (ShowToolsWindow)
		{
			CenterNextWindowIfRequested(ref _centerToolsWindowNextFrame, new Vector2(620f, 430f));
			ImGui.SetNextWindowSize(new Vector2(620f, 430f), ImGuiCond.FirstUseEver);
			if (ImGui.Begin(Tr("window.tools", "Tools"), ref ShowToolsWindow))
			{
				ImGui.BeginChild("Sidebar", new Vector2(100f, 0f), ImGuiChildFlags.Borders);
				if (ImGui.Selectable(Tr("tools.tab.optimization", "优化"), _currentToolTab == 0))
				{
					_currentToolTab = 0;
				}
				if (ImGui.Selectable(Tr("tools.tab.visual", "视觉"), _currentToolTab == 1))
				{
					_currentToolTab = 1;
				}
				if (ImGui.Selectable(Tr("tools.tab.gameUi", "\u6E38\u620F UI"), _currentToolTab == 2))
				{
					_currentToolTab = 2;
				}
				ImGui.EndChild();
				ImGui.SameLine();
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
					ImGui.Text(Tr("tools.greenPlanet", "？之行星"));
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
			CenterNextWindowIfRequested(ref _centerKeyviewerWindowNextFrame, new Vector2(550f, 450f));
			ImGui.SetNextWindowSize(new Vector2(550f, 450f), ImGuiCond.FirstUseEver);
			if (ImGui.Begin(Tr("window.keyViewer", "KeyViewer"), ref ShowKeyviewerWindow))
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
					Main.RequestSave();
				}
				bool v5 = Main.Settings.LimitInput;
				if (ImGui.Checkbox(Tr("kv.limitInput", "限制输入"), ref v5))
				{
					Main.Settings.LimitInput = v5;
					Main.RequestSave();
					InputInterceptor.UpdateAllowedKeys();
				}
				ImGui.SameLine(ImGui.GetWindowWidth() - 150f);
				if (ImGui.Button(Tr("kv.resetStats", "重置统计数据")) && (Object)KeyViewerManager.Instance != (Object)null)
				{
					KeyViewerManager.Instance.ResetCounts();
				}
				if (ImGui.Button(Tr("kv.export", "导出 KV 配置 (.ctkv)")))
				{
					ExportKeyViewerPackage();
				}
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
			ImGui.End();
		}
		if (ShowOverlayerWindow)
		{
			CenterNextWindowIfRequested(ref _centerOverlayerWindowNextFrame, new Vector2(450f, 450f));
			ImGui.SetNextWindowSize(new Vector2(450f, 450f), ImGuiCond.FirstUseEver);
			if (ImGui.Begin(Tr("window.overlayer", "Overlayer"), ref ShowOverlayerWindow))
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
					Main.RequestSave();
				}
				ImGui.SameLine(ImGui.GetWindowWidth() - 120f);
				bool v23 = Main.Settings.OverlayerEditMode;
				if (ImGui.Checkbox(Tr("ov.unlockDrag", "解锁拖动"), ref v23))
				{
					Main.Settings.OverlayerEditMode = v23;
					Main.RequestSave();
				}
				if (ImGui.Button(Tr("ov.export", "导出 OV 配置 (.ctov)")))
				{
					ExportOverlayerPackage();
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
						ImGui.BeginChild("OvSidebar", new Vector2(120f, 0f), ImGuiChildFlags.Borders);
						if (ImGui.Button(Tr("ov.newText", "新建文本 (+)"), new Vector2(-1f, 0f)))
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
									ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), "公式: " + SingleLinePreview(FormatPreviewText(overlayerText2.TextFormat ?? string.Empty), 72));
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
									DrawOverlayerTextPreview(overlayerText2, selectedOvSidebarTab, $"preview_ov_{selectedOvSidebarTab}", new Vector2(0f, 60f));
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
						ImGui.BeginChild("OvImgSidebar", new Vector2(120f, 0f), ImGuiChildFlags.Borders);
						if (ImGui.Button(Tr("ov.newImage", "新建图片 (+)"), new Vector2(-1f, 0f)))
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

						ImGui.BeginChild("OvVideoSidebar", new Vector2(140f, 0f), ImGuiChildFlags.Borders);
						bool canAddVideo = overlayerVideos.Count < 2;
						if (!canAddVideo) ImGui.BeginDisabled();
						if (ImGui.Button(Tr("ov.newVideo", "新建视频 (+)"), new Vector2(-1f, 0f)))
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

						ImGui.BeginChild("OvBarSidebar", new Vector2(140f, 0f), ImGuiChildFlags.Borders);
						if (ImGui.Button(Tr("ov.newProgressBar", "新建进度条 (+)"), new Vector2(-1f, 0f)))
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
			ImGui.End();
		}
		if (ShowHelpWindow)
		{
			DrawHelpWindow();
		}
		DrawOverlayerTextEditorWindow();
		if (!ShowSettingsWindow)
		{
			return;
		}
		CenterNextWindowIfRequested(ref _centerSettingsWindowNextFrame, new Vector2(480f, 360f));
		ImGui.SetNextWindowSize(new Vector2(480f, 360f), ImGuiCond.FirstUseEver);
		if (ImGui.Begin(Tr("settings.title", "设置"), ref ShowSettingsWindow))
		{
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
			DrawWindowResetSettings();
			ImGui.Separator();
			DrawLocalizationSettings();
			ImGui.Separator();
			DrawUpdateSettings();
			ImGui.Separator();
			DrawCloudSyncSection();
			ImGui.Separator();
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

			ImGui.Separator();
			ImGui.Text(Tr("settings.integration", "\u8054\u52A8\u8BBE\u7F6E"));
			bool xPerfectActive = XPerfectBridge.Active;
			ImGui.TextColored(
				xPerfectActive ? new System.Numerics.Vector4(0.3f, 1f, 0.45f, 1f) : new System.Numerics.Vector4(1f, 0.45f, 0.35f, 1f),
				xPerfectActive ? Tr("settings.xperfectDetected", "XPerfect \u5DF2\u68C0\u6D4B\u5230") : Tr("settings.xperfectMissing", "XPerfect \u672A\u68C0\u6D4B\u5230\u6216\u672A\u542F\u7528")
			);
			ImGui.SameLine();
			if (ImGui.Button(Tr("settings.refreshDetection", "\u5237\u65B0\u68C0\u6D4B") + "##XPerfectRefresh"))
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

			ImGui.Separator();
			ImGui.Text(Tr("settings.developerOptions", "\u5F00\u53D1\u8005\u9009\u9879"));
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
			ImGui.Separator();
			ImGui.Spacing();
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
						CheryToolsAssets.ImportCytPackage(path, destFileName);
						ReloadSettingsAfterImport(path);
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
			ImGui.Separator();
			ImGui.Spacing();
			if (ImGui.Button(Tr("settings.closeMenu", "关闭菜单")))
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
