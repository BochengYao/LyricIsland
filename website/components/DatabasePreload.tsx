type Props = {
  href: "/api/features" | "/api/incentives/public" | "/api/incentives/public?preview_limit=50";
};

export function DatabasePreload({ href }: Props) {
  const script = `(() => {
    const url = ${JSON.stringify(href)};
    const requests = window.__LYRIC_ISLAND_DATA_PRELOADS__ ||= {};
    const existing = requests[url];
    if (existing && Date.now() - existing.startedAt < 15000) return;
    const promise = fetch(url, {
      credentials: "include",
      headers: { Accept: "application/json" }
    }).then(async (response) => {
      if (!response.ok) throw new Error("Unable to load " + url);
      return response.json();
    });
    requests[url] = { startedAt: Date.now(), promise };
    void promise.catch(() => {
      if (requests[url]?.promise === promise) delete requests[url];
    });
  })();`;

  return <script dangerouslySetInnerHTML={{ __html: script }} />;
}
