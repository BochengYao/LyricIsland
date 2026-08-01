type CachedRequest = {
  startedAt: number;
  promise: Promise<unknown>;
};

declare global {
  interface Window {
    __LYRIC_ISLAND_DATA_PRELOADS__?: Record<string, CachedRequest>;
  }
}

const REQUEST_TTL_MS = 15_000;
const requests = new Map<string, CachedRequest>();

export function preloadClientJson<T>(url: string): Promise<T> | null {
  if (typeof window === "undefined") return null;

  const browserRequests = window.__LYRIC_ISLAND_DATA_PRELOADS__ ??= {};
  const existing = requests.get(url) ?? browserRequests[url];
  if (existing && Date.now() - existing.startedAt < REQUEST_TTL_MS) {
    requests.set(url, existing);
    return existing.promise as Promise<T>;
  }

  const promise = fetch(url, {
    credentials: "include",
    headers: { Accept: "application/json" }
  }).then(async (response) => {
    if (!response.ok) throw new Error(`Unable to load ${url}`);
    return await response.json() as T;
  }).catch((error) => {
    requests.delete(url);
    if (browserRequests[url]?.promise === promise) delete browserRequests[url];
    throw error;
  });

  const cached = { startedAt: Date.now(), promise };
  requests.set(url, cached);
  browserRequests[url] = cached;
  return promise;
}
