const discoveryTimeoutMs = 5000;

async function loadDiscovery() {
  const status = document.querySelector("#connection-status");
  const details = document.querySelector("#protocol-details");
  const retry = document.querySelector("#retry-discovery");
  const controller = new AbortController();
  const timeout = window.setTimeout(() => controller.abort(), discoveryTimeoutMs);
  retry.hidden = false;
  retry.disabled = true;
  status.classList.remove("error");
  status.textContent = "Checking protocol discovery…";
  try {
    const response = await fetch("/api/v1/handshake", {
      headers: { Accept: "application/json" },
      signal: controller.signal,
    });
    if (!response.ok) throw new Error("handshake returned " + response.status);
    const handshake = await response.json();
    const values = [
      ["protocol", handshake.protocol.major + "." + handshake.protocol.minor],
      ["discovery capability", handshake.serverCapabilities.join(", ") || "none"],
      ["world observation", "paired owner device only"],
    ];
    details.replaceChildren(...values.flatMap(([name, value]) => {
      const term = document.createElement("dt"); term.textContent = name;
      const detail = document.createElement("dd"); detail.textContent = value;
      return [term, detail];
    }));
    status.textContent = "Protocol discovery available · paired observation required";
  } catch (error) {
    status.textContent = error.name === "AbortError"
      ? "Protocol discovery timed out · check the server and retry"
      : "Unable to load protocol discovery: " + error.message;
    status.classList.add("error");
  } finally {
    window.clearTimeout(timeout);
    retry.disabled = false;
  }
}

document.querySelector("#retry-discovery").addEventListener("click", loadDiscovery);
loadDiscovery();
