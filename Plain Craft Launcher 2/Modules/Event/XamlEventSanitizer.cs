using System.Text.RegularExpressions;

namespace PCL
{
    public static partial class XamlEventSanitizer
    {
        public class SanitizeResult
        {
            public string SanitizedXaml { get; set; } = "";
            public List<string> UnsupportedTypesFound { get; set; } = new();
            public List<string> UnrecognizedTypes { get; set; } = new();
        }

        [GeneratedRegex(@"(local:CustomEventService\.EventType\s*=\s*"")([^""]+)("")")]
        private static partial Regex EventTypeAttributeRegex();

        [GeneratedRegex(@"(<local:CustomEventService\.EventType\s*>\s*)([^<]+?)(\s*</local:CustomEventService\.EventType\s*>)")]
        private static partial Regex EventTypePropertyElementRegex();

        [GeneratedRegex(@"(<local:CustomEvent\.Type\s*>\s*)([^<]+?)(\s*</local:CustomEvent\.Type\s*>)")]
        private static partial Regex CustomEventTypePropertyElementRegex();

        [GeneratedRegex(@"(<local:CustomEvent\s+[^>]*?\bType\s*=\s*"")([^""]+)("")")]
        private static partial Regex LocalCustomEventTypeAttributeRegex();

        [GeneratedRegex(@"<local:CustomEvent\s+[^>]*?\bType\s*=\s*""([^""]+)""[^>]*/\s*>")]
        private static partial Regex LocalCustomEventSelfClosingRegex();

        [GeneratedRegex(@"<local:CustomEvent\s+[^>]*?\bType\s*=\s*""([^""]+)""[^>]*>")]
        private static partial Regex LocalCustomEventOpenTagRegex();

        [GeneratedRegex(@"<Setter\b[^>]*?\bProperty\s*=\s*""local:CustomEventService\.EventType""[^>]*?\bValue\s*=\s*""([^""]+)""[^>]*(?:/\s*>|>\s*</Setter\s*>)")]
        private static partial Regex SetterEventTypePropertyFirstRegex();

        [GeneratedRegex(@"<Setter\b[^>]*?\bValue\s*=\s*""([^""]+)""[^>]*?\bProperty\s*=\s*""local:CustomEventService\.EventType""[^>]*(?:/\s*>|>\s*</Setter\s*>)")]
        private static partial Regex SetterEventTypeValueFirstRegex();

        /// <summary>
        ///     清理 XAML 中的自定义事件声明。
        /// </summary>
        /// <param name="xaml">待清理的 XAML 文本。</param>
        /// <param name="stripAllEvents">
        ///     为 true 时剥离所有事件动作，只保留展示内容。
        ///     用于来自网络的页面：本地优先策略下这类内容不允许驱动任何本地行为。
        /// </param>
        public static SanitizeResult Sanitize(string xaml, bool stripAllEvents = false)
        {
            var result = new SanitizeResult();
            var sanitized = xaml;

            sanitized = EventTypeAttributeRegex().Replace(sanitized, match =>
            {
                var rawValue = match.Groups[2].Value;
                return ReplaceEventType(match, rawValue, result, stripAllEvents);
            });

            sanitized = EventTypePropertyElementRegex().Replace(sanitized, match =>
            {
                var rawValue = match.Groups[2].Value.Trim();
                return ReplaceEventType(match, rawValue, result, stripAllEvents);
            });

            sanitized = CustomEventTypePropertyElementRegex().Replace(sanitized, match =>
            {
                var rawValue = match.Groups[2].Value.Trim();
                return ReplaceOrRemovePropertyElement(match, rawValue, result, stripAllEvents);
            });

            sanitized = LocalCustomEventTypeAttributeRegex().Replace(sanitized, match =>
            {
                var rawValue = match.Groups[2].Value;
                return ReplaceEventType(match, rawValue, result, stripAllEvents);
            });

            sanitized = LocalCustomEventSelfClosingRegex().Replace(sanitized, match =>
            {
                var rawValue = match.Groups[1].Value;
                return _RemoveBadCustomEventElement(match, rawValue, result, stripAllEvents);
            });

            sanitized = LocalCustomEventOpenTagRegex().Replace(sanitized, match =>
            {
                var rawValue = match.Groups[1].Value;
                if (_IsBadType(rawValue, result, stripAllEvents))
                {
                    var elementName = "local:CustomEvent";
                    var afterTag = sanitized[(match.Index + match.Length)..];
                    var closeLen = FindMatchingCloseTag(afterTag, elementName);
                    if (closeLen < 0) return match.Value;
                    return afterTag[closeLen..];
                }
                return match.Value;
            });

            sanitized = SetterEventTypePropertyFirstRegex().Replace(sanitized, match =>
                _ReplaceSetter(match, match.Groups[1].Value, result, stripAllEvents));

            sanitized = SetterEventTypeValueFirstRegex().Replace(sanitized, match =>
                _ReplaceSetter(match, match.Groups[1].Value, result, stripAllEvents));

            RemoveElementsForTypes(result.UnsupportedTypesFound, ref sanitized);
            RemoveElementsForTypes(result.UnrecognizedTypes, ref sanitized);

            result.UnsupportedTypesFound = result.UnsupportedTypesFound.Distinct().ToList();
            result.UnrecognizedTypes = result.UnrecognizedTypes.Distinct().ToList();

            result.SanitizedXaml = sanitized;
            return result;
        }

        private static bool _IsBadType(string rawValue, SanitizeResult result, bool stripAllEvents)
        {
            if (stripAllEvents)
            {
                // 全量剥离：不区分类型，一律按“不受支持”处理，交给后续按值删除元素。
                result.UnsupportedTypesFound.Add(rawValue);
                return true;
            }

            if (EventTypeMapper.IsUnsupportedType(rawValue))
            {
                result.UnsupportedTypesFound.Add(rawValue);
                return true;
            }
            if (!Enum.TryParse<EventType>(rawValue, true, out _)
                && !EventTypeMapper.TryToEnglish(rawValue, out _))
            {
                result.UnrecognizedTypes.Add(rawValue);
                return true;
            }
            return false;
        }

        private static string _RemoveBadCustomEventElement(
            Match match, string rawValue, SanitizeResult result, bool stripAllEvents)
        {
            return _IsBadType(rawValue, result, stripAllEvents) ? "" : match.Value;
        }

        private static void RemoveElementsForTypes(List<string> types, ref string sanitized)
        {
            var snapshot = types.ToList();
            foreach (var type in snapshot)
                sanitized = RemoveElementsWithEventType(sanitized, type, types);
        }

        private static string ReplaceEventType(Match match, string rawValue, SanitizeResult result,
            bool stripAllEvents)
        {
            // 全量剥离时不做中英文转换：后续按值删除元素需要与原文保持一致。
            if (stripAllEvents)
            {
                result.UnsupportedTypesFound.Add(rawValue);
                return match.Value;
            }

            if (EventTypeMapper.TryToEnglish(rawValue, out var englishName))
                return match.Groups[1].Value + englishName + match.Groups[3].Value;

            if (Enum.TryParse<EventType>(rawValue, true, out _))
                return match.Value;

            if (EventTypeMapper.IsUnsupportedType(rawValue))
            {
                result.UnsupportedTypesFound.Add(rawValue);
                return match.Value;
            }

            result.UnrecognizedTypes.Add(rawValue);
            return match.Value;
        }

        private static string ReplaceOrRemovePropertyElement(
            Match match, string rawValue, SanitizeResult result, bool stripAllEvents)
        {
            if (_IsBadType(rawValue, result, stripAllEvents))
                return "";
            return ReplaceEventType(match, rawValue, result, stripAllEvents);
        }

        private static string _ReplaceSetter(Match match, string rawValue, SanitizeResult result,
            bool stripAllEvents)
        {
            if (_IsBadType(rawValue, result, stripAllEvents))
                return "";
            if (EventTypeMapper.TryToEnglish(rawValue, out var englishName))
                return match.Value.Replace($"Value=\"{rawValue}\"", $"Value=\"{englishName}\"");
            return match.Value;
        }

        private static string RemoveElementsWithEventType(string xaml, string eventTypeValue, List<string> trackingList)
        {
            var escaped = Regex.Escape(eventTypeValue);

            var selfClosingPattern = $@"<[\w:]+[^>]*\s+local:CustomEventService\.EventType\s*=\s*""{escaped}""[^>]*/\s*>";
            xaml = Regex.Replace(xaml, selfClosingPattern, match =>
            {
                trackingList.Add(eventTypeValue);
                return "";
            }, RegexOptions.Compiled);

            var openTagPattern = $@"<[\w:]+[^>]*\s+local:CustomEventService\.EventType\s*=\s*""{escaped}""[^>]*>";
            xaml = Regex.Replace(xaml, openTagPattern, match =>
            {
                var elementName = Regex.Match(match.Value, @"<([\w:]+)").Groups[1].Value;
                var afterTag = xaml[(match.Index + match.Length)..];
                var closeLen = FindMatchingCloseTag(afterTag, elementName);
                if (closeLen < 0) return match.Value;

                trackingList.Add(eventTypeValue);
                return afterTag[closeLen..];
            }, RegexOptions.Compiled);

            var propertyElementPattern = $@"<local:CustomEventService\.EventType\s*>\s*{escaped}\s*</local:CustomEventService\.EventType\s*>";
            xaml = Regex.Replace(xaml, propertyElementPattern, match =>
            {
                var beforeMatch = xaml[..match.Index];
                // 不能用 RegexOptions.RightToLeft：RTL 下 ([\w:]+) 只捕获到标签名的最后一个字符，
                // 父元素名会变成 "l"，导致找不到闭合标签、属性元素永远删不掉。改为取最靠后的完整标签。
                var openTags = Regex.Matches(beforeMatch, @"<([\w:]+)[^>]*>");
                if (openTags.Count == 0) return match.Value;
                var parentElementName = openTags[^1].Groups[1].Value;

                var afterProperty = xaml[(match.Index + match.Length)..];
                var parentCloseMatch = Regex.Match(afterProperty, $@"</{parentElementName}\s*>");
                if (!parentCloseMatch.Success) return match.Value;

                trackingList.Add(eventTypeValue);
                return "";
            }, RegexOptions.Compiled);

            return xaml;
        }

        private static int FindMatchingCloseTag(string text, string elementName)
        {
            var closePattern = $@"</{elementName}\s*>";
            var closeMatch = Regex.Match(text, closePattern);
            return closeMatch.Success ? closeMatch.Index + closeMatch.Length : -1;
        }
    }
}
