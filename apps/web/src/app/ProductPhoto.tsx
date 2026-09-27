import { useEffect, useState } from "react";
import { Package } from "lucide-react";

// Product image with a neutral fallback; shared by the catalogue screen and the register.
export function ProductPhoto({
  src,
  name,
  className = "h-16 w-16",
}: {
  src?: string | null;
  name: string;
  className?: string;
}) {
  const [failed, setFailed] = useState(false);
  useEffect(() => setFailed(false), [src]);
  return (
    <div
      className={`${className} shrink-0 overflow-hidden rounded-xl bg-accent text-primary`}
    >
      {src && !failed ? (
        <img
          src={src}
          alt={name}
          loading="lazy"
          onError={() => setFailed(true)}
          className="h-full w-full object-contain"
        />
      ) : (
        <span className="flex h-full items-center justify-center" title={name}>
          <Package size={28} />
        </span>
      )}
    </div>
  );
}
