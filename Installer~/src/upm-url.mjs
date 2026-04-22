/**
 * Normalize a Unity UPM-style git URL into the native form accepted by `git clone`.
 *
 * UPM accepts URLs like:
 *   git+https://github.com/user/repo.git
 *   git+ssh://git@github.com/user/repo.git#feature-branch
 *   git+file:///path/to/repo#v1.2.3
 *   https://github.com/user/repo.git?path=Packages/Sub#main
 *
 * Native `git clone` cannot parse the `git+` scheme prefix (it treats it as a
 * remote helper name), ignores `#fragment` (must be `--branch <ref>`), and does
 * not know about UPM's `?path=` query.
 *
 * @param {string} upmUrl UPM-formatted dependency URL.
 * @returns {{ url: string, ref: string | null }} Native git URL and optional ref.
 */
export function parseUpmUrl(upmUrl) {
  if (typeof upmUrl !== "string" || upmUrl.length === 0) {
    throw new Error("parseUpmUrl: empty url");
  }
  let url = upmUrl.startsWith("git+") ? upmUrl.slice(4) : upmUrl;

  let ref = null;
  const hashIdx = url.indexOf("#");
  if (hashIdx >= 0) {
    ref = url.slice(hashIdx + 1) || null;
    url = url.slice(0, hashIdx);
  }

  const qIdx = url.indexOf("?");
  if (qIdx >= 0) {
    url = url.slice(0, qIdx);
  }

  return { url, ref };
}
