import { spawnSync } from "node:child_process";

export function git(cwd, args) {
  const r = spawnSync("git", args, { cwd, encoding: "utf8" });
  return {
    exitCode: r.status ?? 1,
    stdout: r.stdout ?? "",
    stderr: r.stderr ?? "",
  };
}

export function gitOk(cwd, args) {
  return git(cwd, args).exitCode === 0;
}
