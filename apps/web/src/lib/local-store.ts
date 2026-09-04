export function createLocalStore(namespace: string) {
  const key = (name: string) => `${namespace}:${name}`;

  return {
    get<T>(name: string): T | null {
      try {
        const value = window.localStorage.getItem(key(name));
        return value === null ? null : (JSON.parse(value) as T);
      } catch {
        return null;
      }
    },
    set<T>(name: string, value: T): boolean {
      try {
        window.localStorage.setItem(key(name), JSON.stringify(value));
        return true;
      } catch {
        return false;
      }
    },
    remove(name: string): void {
      try {
        window.localStorage.removeItem(key(name));
      } catch {
        // Storage can be unavailable in private browsing or restricted contexts.
      }
    },
  };
}

export function createId(): string {
  if (typeof crypto !== "undefined" && typeof crypto.randomUUID === "function") {
    return crypto.randomUUID();
  }

  const bytes = new Uint8Array(16);
  crypto.getRandomValues(bytes);
  bytes[6] = (bytes[6] & 0x0f) | 0x40;
  bytes[8] = (bytes[8] & 0x3f) | 0x80;
  const hex = [...bytes].map((byte) => byte.toString(16).padStart(2, "0")).join("");
  return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}`;
}

const store = createLocalStore("ofc");

export function getLocalId(name: string): string {
  const existing = store.get<string>(name);
  if (existing) return existing;

  const id = createId();
  store.set(name, id);
  return id;
}

export { store };
