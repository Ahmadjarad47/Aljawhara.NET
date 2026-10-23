/** Contract for the separate storefront. This module does not implement or imitate a shopping cart. */
export interface Recommendation {
  productId: number; variantId: null; productName: string; imageUrl: string | null;
  unitPrice: number; currency: "KWD"; unitsPerPackage: number | null;
  recommendedPackageQuantity: number; lineTotal: number; reason: string; needKeys: string[];
}
export interface ShoppingResponse {
  conversationId: string; recommendationId: string; version: number; message: string;
  status: "ready" | "empty" | "clarification"; recommendations: Recommendation[];
  currency: "KWD"; estimatedTotal: number; unavailableNeeds: string[];
  requiresConfirmation: boolean; expiresAtUtc: string;
  usage: { inputTokens: number; outputTokens: number; embeddingInputTokens: number; estimatedUsd: number; durationMs: number };
}
export interface Confirmation {
  operationId: string; status: "added" | "partial" | "unavailable";
  items: { productId: number; requestedQuantity: number; addedQuantity: number; errorCode: string | null }[];
}
export interface PreparedBrowserCart {
  operationId: string; status: "prepared"; expiresAtUtc: string; replayUntilUtc: string;
  items: { productId: number; name: string; image: string | null; price: number; quantity: number; availablePackages: number }[];
  unavailableItems: Confirmation["items"];
}
export class ShoppingApiError extends Error {
  constructor(public status: number, public code: string, public traceId?: string) { super(code); }
}
export class AIShoppingClient {
  private guestToken: string | null = null;
  constructor(private baseUrl: string, private bearerToken: () => string | null) {}
  async startGuestSession(signal?: AbortSignal): Promise<void> {
    const response = await this.request<{ token: string }>("/sessions", "POST", undefined, signal);
    this.guestToken = response.token;
  }
  clearSession(): void { this.guestToken = null; }
  chat(message: string, previous?: ShoppingResponse, signal?: AbortSignal): Promise<ShoppingResponse> {
    return this.request("/chat", "POST", { message, conversationId: previous?.conversationId ?? null,
      expectedVersion: previous?.version ?? null }, signal);
  }
  load(id: string, signal?: AbortSignal): Promise<ShoppingResponse> {
    return this.request(`/conversations/${encodeURIComponent(id)}`, "GET", undefined, signal);
  }
  recalculate(previous: ShoppingResponse, items: { productId: number; packageQuantity: number }[], signal?: AbortSignal): Promise<ShoppingResponse> {
    return this.request(`/conversations/${encodeURIComponent(previous.conversationId)}/recalculate`, "POST",
      { expectedVersion: previous.version, items }, signal);
  }
  /** Existing browser storefront: prepare after explicit approval, then commit through its actual CartService. This call does not add items. */
  prepareApprovedCart(previous: ShoppingResponse, currentCart: { productId: number; quantity: number }[], signal?: AbortSignal): Promise<PreparedBrowserCart> {
    return this.request(`/conversations/${encodeURIComponent(previous.conversationId)}/recommendations/${encodeURIComponent(previous.recommendationId)}/prepare-cart`,
      "POST", { expectedVersion: previous.version, approved: true, currentCart }, signal);
  }
  /** Optional FUTURE server-cart integration only; currently closed and unused by the Angular storefront. */
  confirmApproved(previous: ShoppingResponse, signal?: AbortSignal): Promise<Confirmation> {
    return this.request(`/conversations/${encodeURIComponent(previous.conversationId)}/recommendations/${encodeURIComponent(previous.recommendationId)}/confirm`,
      "POST", { expectedVersion: previous.version, approved: true }, signal);
  }
  private async request<T>(path: string, method: string, body?: unknown, signal?: AbortSignal): Promise<T> {
    const headers = new Headers({ "Accept": "application/json" });
    const token = this.bearerToken();
    if (token) headers.set("Authorization", `Bearer ${token}`);
    else if (this.guestToken) headers.set("X-AI-Shopping-Session", this.guestToken);
    if (body !== undefined) headers.set("Content-Type", "application/json");
    const response = await fetch(`${this.baseUrl.replace(/\/$/, "")}/api/ai-shopping${path}`, {
      method, headers, credentials: "omit", cache: "no-store", body: body === undefined ? undefined : JSON.stringify(body), signal,
    });
    if (!response.ok) {
      const error = await response.json().catch(() => ({}));
      throw new ShoppingApiError(response.status, error.code ?? error.title ?? "REQUEST_FAILED", error.traceId);
    }
    return response.json() as Promise<T>;
  }
}
export const kwd = (amount: number): string => new Intl.NumberFormat("ar-KW", {
  style: "currency", currency: "KWD", minimumFractionDigits: 3, maximumFractionDigits: 3,
}).format(amount);

/** Safe text example. The real storefront supplies its styles, product route helper, images, inputs and cart state. */
export function renderRecommendationText(container: HTMLElement, response: ShoppingResponse): void {
  container.dir = "rtl"; container.lang = "ar"; container.replaceChildren();
  const message = document.createElement("p"); message.textContent = response.message; container.append(message);
  for (const product of response.recommendations) {
    const text = document.createElement("p");
    text.textContent = `${product.productName} — ${product.recommendedPackageQuantity} عبوة — ${kwd(product.lineTotal)}`;
    container.append(text);
  }
}
