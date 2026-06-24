#!/usr/bin/env bash
#
# Readiness check for the interview-dotnet-api live coding round.
# Verifies your machine can build and run this .NET 10 project before the session.
#
# Usage:
#   ./check-readiness.sh            # run all checks
#   ./check-readiness.sh --skip-run # skip the launch/HTTP check
#
set -u

SKIP_RUN=0
[ "${1:-}" = "--skip-run" ] && SKIP_RUN=1

# --- pretty output ---------------------------------------------------------
if [ -t 1 ]; then
  GREEN=$'\033[0;32m'; RED=$'\033[0;31m'; YELLOW=$'\033[0;33m'; BOLD=$'\033[1m'; RESET=$'\033[0m'
else
  GREEN=""; RED=""; YELLOW=""; BOLD=""; RESET=""
fi

REQUIRED_FAILED=0
pass()  { echo "  ${GREEN}✔ PASS${RESET}  $1"; }
fail()  { echo "  ${RED}✗ FAIL${RESET}  $1"; REQUIRED_FAILED=1; }
warn()  { echo "  ${YELLOW}• NOTE${RESET}  $1"; }
section()  { echo; echo "${BOLD}$1${RESET}"; }

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
cd "$PROJECT_DIR" || exit 1
OPENAPI_PORT=5080
DLL="bin/Debug/net10.0/interview-dotnet-api.dll"

echo "${BOLD}interview-dotnet-api — device readiness check${RESET}"
echo "Project: $PROJECT_DIR"

# --- 1. .NET 10 SDK --------------------------------------------------------
section "1. .NET 10 SDK"
if ! command -v dotnet >/dev/null 2>&1; then
  fail "'dotnet' not found on PATH. Install the .NET 10 SDK: https://dotnet.microsoft.com/download/dotnet/10.0"
else
  SDK_LINE=$(dotnet --list-sdks | grep '^10\.' | head -1)
  if [ -n "$SDK_LINE" ]; then
    SDK_VER=${SDK_LINE%% *}
    pass "found $SDK_VER (dotnet --version: $(dotnet --version))"
  else
    HAVE=$(dotnet --list-sdks | sed 's/ .*//' | tr '\n' ',' | sed 's/,$//')
    fail "no 10.x SDK installed. Have: ${HAVE:-none}. Get .NET 10: https://dotnet.microsoft.com/download/dotnet/10.0"
  fi
fi

# --- 2. EF Core CLI tools --------------------------------------------------
section "2. EF Core CLI tools"
if dotnet ef --version >/dev/null 2>&1; then
  pass "dotnet ef $(dotnet ef --version 2>/dev/null | tail -1)"
else
  fail "'dotnet ef' not available. Install: dotnet tool install --global dotnet-ef"
fi

# --- 3. IDE (informational) ------------------------------------------------
section "3. IDE"
IDE_FOUND=0
found_ide() { pass "$1"; IDE_FOUND=1; }

# CLI launchers on PATH
command -v code   >/dev/null 2>&1 && found_ide "VS Code ('code' on PATH) detected — install the C# Dev Kit extension"
command -v rider  >/dev/null 2>&1 && found_ide "Rider ('rider' on PATH) detected"
command -v devenv >/dev/null 2>&1 && found_ide "Visual Studio ('devenv' on PATH) detected"

# Standard install locations (the CLI launcher is often not on PATH)
case "$(uname -s)" in
  Darwin)
    [ -d "/Applications/Visual Studio Code.app" ] && found_ide "VS Code app found in /Applications — install the C# Dev Kit extension"
    for app in /Applications/Rider*.app "$HOME/Applications"/Rider*.app "$HOME/Applications/JetBrains Toolbox"/Rider*.app; do
      [ -d "$app" ] && { found_ide "Rider app found ($app)"; break; }
    done
    ;;
  MINGW*|MSYS*|CYGWIN*)
    [ -f "$LOCALAPPDATA/Programs/Microsoft VS Code/Code.exe" ] && found_ide "VS Code found in %LOCALAPPDATA% — install the C# Dev Kit extension"
    ;;
esac

[ "$IDE_FOUND" -eq 0 ] && warn "No IDE auto-detected (checks PATH + standard install paths). If you already have Visual Studio, VS Code (+ C# Dev Kit), or Rider installed, you're fine — this check can miss non-standard locations."

# --- 4. HTTP client (informational) ----------------------------------------
section "4. HTTP client"
if command -v curl >/dev/null 2>&1; then
  pass "curl detected"
else
  warn "curl not found — Swagger UI, Postman, or a .http file in your IDE work too."
fi

# --- 5. AI assistant (reminder) --------------------------------------------
# NOTE: sign-in state can't be detected from a script (it lives in browser
# cookies or an extension's private store). We can only detect *installed* tooling.
section "5. AI assistant"
AI_FOUND=""
command -v claude >/dev/null 2>&1 && AI_FOUND="$AI_FOUND Claude Code CLI;"
command -v cursor >/dev/null 2>&1 && AI_FOUND="$AI_FOUND Cursor CLI;"
if command -v code >/dev/null 2>&1; then
  EXTS=$(code --list-extensions 2>/dev/null | grep -iE 'copilot|claude|continue|codeium|sourcegraph.cody|tabnine' | tr '\n' ',' | sed 's/,$//')
  [ -n "$EXTS" ] && AI_FOUND="$AI_FOUND VS Code extensions: $EXTS;"
fi
command -v gh >/dev/null 2>&1 && gh extension list 2>/dev/null | grep -qi copilot && AI_FOUND="$AI_FOUND gh copilot;"
[ "$(uname -s)" = "Darwin" ] && [ -d "/Applications/Cursor.app" ] && AI_FOUND="$AI_FOUND Cursor app;"

if [ -n "$AI_FOUND" ]; then
  warn "Detected AI tooling:$AI_FOUND that's installation only — confirm you're actually signed in (sign-in state can't be verified from a script)."
else
  warn "No AI tooling auto-detected, and sign-in state can't be verified from a script. Make sure your assistant (browser tab or IDE extension) is installed and signed in."
fi

# --- 6. Build --------------------------------------------------------------
section "6. Build"
BUILD_OK=0
if dotnet build -v q >/tmp/ifa_build.log 2>&1; then
  pass "dotnet build compiles cleanly"
  BUILD_OK=1
else
  fail "dotnet build failed. Output:"; sed 's/^/        /' /tmp/ifa_build.log | tail -20
fi

# --- 7. Run + OpenAPI ------------------------------------------------------
section "7. Run & serve OpenAPI"
if [ "$SKIP_RUN" -eq 1 ]; then
  warn "skipped (--skip-run)"
elif [ "$BUILD_OK" -eq 0 ]; then
  warn "skipped (build did not succeed)"
elif [ ! -f "$DLL" ]; then
  warn "skipped (built assembly not found at $DLL)"
else
  # Run the built assembly directly so it's a single process we can stop cleanly.
  ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS="http://localhost:$OPENAPI_PORT" \
    dotnet "$DLL" >/tmp/ifa_run.log 2>&1 &
  RUN_PID=$!

  URL="http://localhost:$OPENAPI_PORT/openapi/v1.json"
  OK=0
  for _ in $(seq 1 30); do
    if ! kill -0 "$RUN_PID" 2>/dev/null; then break; fi   # process died
    if command -v curl >/dev/null 2>&1 && curl -fsS -o /dev/null "$URL" 2>/dev/null; then OK=1; break; fi
    sleep 1
  done

  kill "$RUN_PID" >/dev/null 2>&1; wait "$RUN_PID" 2>/dev/null
  if [ "$OK" -eq 1 ]; then
    pass "app started and served OpenAPI at $URL"
  else
    fail "could not reach $URL within 30s. Run log:"; sed 's/^/        /' /tmp/ifa_run.log | tail -20
  fi
fi

# --- summary ---------------------------------------------------------------
echo
if [ "$REQUIRED_FAILED" -eq 0 ]; then
  echo "${GREEN}${BOLD}All required checks passed — you're ready for the round.${RESET}"
  exit 0
else
  echo "${RED}${BOLD}Some required checks failed — please resolve the items marked FAIL above.${RESET}"
  exit 1
fi
