export type TranslationEntry = {
  key: string;
  text: string;
};

export type TranslationResult = Record<string, Record<string, string>>;

const MAX_ENTRIES = 80;
const MAX_TEXT_LENGTH = 2_400;

function translationConfig() {
  const apiKey = process.env.DEEPSEEK_API_KEY;
  if (!apiKey) throw new Error("DEEPSEEK_API_KEY is not configured");
  return {
    apiKey,
    baseUrl: (process.env.DEEPSEEK_BASE_URL || "https://api.deepseek.com").replace(/\/$/, ""),
    model: process.env.DEEPSEEK_MODEL || "deepseek-v4-flash"
  };
}

function cleanEntries(value: unknown): TranslationEntry[] {
  if (!Array.isArray(value)) return [];
  const usedKeys = new Set<string>();
  return value
    .filter((item): item is Record<string, unknown> => Boolean(item) && typeof item === "object")
    .map((item) => ({
      key: typeof item.key === "string" ? item.key.trim().slice(0, 120) : "",
      text: typeof item.text === "string" ? item.text.trim().slice(0, MAX_TEXT_LENGTH) : ""
    }))
    .filter((item) => item.key && item.text && !usedKeys.has(item.key) && (usedKeys.add(item.key), true))
    .slice(0, MAX_ENTRIES);
}

function cleanTargetLocales(value: unknown) {
  if (!Array.isArray(value)) return [];
  const usedLocales = new Set<string>();
  return value
    .filter((item): item is string => typeof item === "string")
    .map((item) => item.trim().toLowerCase().slice(0, 16))
    .filter((item) => /^[a-z]{2,3}(?:-[a-z0-9]{2,8})?$/.test(item) && !usedLocales.has(item) && (usedLocales.add(item), true))
    .slice(0, 8);
}

function parseTranslations(value: unknown, targets: string[], entries: TranslationEntry[]): TranslationResult {
  const source = value && typeof value === "object" ? value as Record<string, unknown> : {};
  const translations = source.translations && typeof source.translations === "object"
    ? source.translations as Record<string, unknown>
    : source;
  const result: TranslationResult = {};
  for (const locale of targets) {
    const language = translations[locale];
    if (!language || typeof language !== "object") throw new Error(`Translation response is missing ${locale}`);
    const fields = language as Record<string, unknown>;
    result[locale] = {};
    for (const entry of entries) {
      const translated = fields[entry.key];
      if (typeof translated !== "string" || !translated.trim()) {
        throw new Error(`Translation response is missing ${locale}.${entry.key}`);
      }
      result[locale][entry.key] = translated.trim().slice(0, MAX_TEXT_LENGTH);
    }
  }
  return result;
}

export async function translateChineseContent(input: {
  entries?: unknown;
  targetLocales?: unknown;
}) {
  const entries = cleanEntries(input.entries);
  const targetLocales = cleanTargetLocales(input.targetLocales);
  if (!entries.length) throw new Error("请先填写中文内容");
  if (!targetLocales.length) throw new Error("请至少选择一种目标语言");

  const config = translationConfig();
  const response = await fetch(`${config.baseUrl}/chat/completions`, {
    method: "POST",
    headers: {
      Authorization: `Bearer ${config.apiKey}`,
      "Content-Type": "application/json"
    },
    body: JSON.stringify({
      model: config.model,
      messages: [
        {
          role: "system",
          content: "You are a localization translator for a software product. Translate Chinese source strings into every requested target locale. Preserve line breaks, list structure, markdown, URLs, code, version numbers, product names, and placeholders exactly when appropriate. Do not add commentary. Return only JSON in this exact shape: {\"translations\": {\"<locale>\": {\"<key>\": \"translated text\"}}}. Every requested locale must contain every input key."
        },
        {
          role: "user",
          content: JSON.stringify({ source_locale: "zh-CN", target_locales: targetLocales, entries })
        }
      ],
      response_format: { type: "json_object" },
      temperature: 0.2,
      max_tokens: 8192,
      stream: false,
      thinking: { type: "disabled" }
    })
  });
  if (!response.ok) throw new Error(`翻译服务暂时不可用（HTTP ${response.status}）`);
  const data = await response.json() as { choices?: Array<{ message?: { content?: string } }> };
  const content = data.choices?.[0]?.message?.content;
  if (!content) throw new Error("翻译服务未返回内容");
  try {
    return { translations: parseTranslations(JSON.parse(content), targetLocales, entries) };
  } catch (error) {
    if (error instanceof Error && error.message.startsWith("Translation response")) throw error;
    throw new Error("翻译结果格式异常，请重试");
  }
}
