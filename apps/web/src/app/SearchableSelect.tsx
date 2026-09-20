import { useMemo, useState } from "react";
import { Check, ChevronsUpDown } from "lucide-react";
import { Button } from "@/components/ui/button";
import {
  Command,
  CommandEmpty,
  CommandGroup,
  CommandInput,
  CommandItem,
  CommandList,
} from "@/components/ui/command";
import { Label } from "@/components/ui/label";
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from "@/components/ui/popover";
import { cn } from "@/lib/utils";

type Option = { value: string; label: string; disabled?: boolean };

// An <option>'s children is often several nodes (e.g. `{sku} · {name}` is three children:
// a string, the literal " · ", and another string), not one plain string — join every
// primitive descendant instead of only handling the single-string-child case, otherwise a
// multi-part label silently falls back to showing the raw id.
function childrenToText(node: React.ReactNode): string {
  if (node === null || node === undefined || typeof node === "boolean")
    return "";
  if (typeof node === "string" || typeof node === "number") return String(node);
  if (Array.isArray(node)) return node.map(childrenToText).join("");
  if (typeof node === "object" && "props" in node)
    return childrenToText(
      (node as { props: { children?: React.ReactNode } }).props.children,
    );
  return "";
}

function optionsFromChildren(children: React.ReactNode): Option[] {
  const options: Option[] = [];
  for (const child of Array.isArray(children)
    ? children.flat(Infinity)
    : [children]) {
    if (!child || typeof child !== "object" || !("props" in child)) continue;
    const props = (
      child as {
        props: {
          value?: string;
          children?: React.ReactNode;
          disabled?: boolean;
        };
      }
    ).props;
    if (props.value === undefined) continue;
    const label = childrenToText(props.children).trim();
    options.push({
      value: String(props.value),
      label: label || String(props.value),
      disabled: props.disabled,
    });
  }
  return options;
}

export function SearchableSelect({
  label,
  value,
  onChange,
  children,
  disabled = false,
  placeholder,
  hideLabel = false,
}: {
  label: string;
  value: string;
  onChange: (value: string) => void;
  children: React.ReactNode;
  disabled?: boolean;
  placeholder?: string;
  hideLabel?: boolean;
}) {
  const options = useMemo(() => optionsFromChildren(children), [children]);
  const [open, setOpen] = useState(false);
  const selected = options.find((o) => o.value === value) ?? null;
  return (
    <div className="min-w-0">
      <Label className={hideLabel ? "sr-only" : "block text-sm font-medium"}>
        {label}
      </Label>
      <Popover open={open} onOpenChange={setOpen}>
        <PopoverTrigger asChild>
          <Button
            type="button"
            variant="outline"
            disabled={disabled}
            role="combobox"
            aria-expanded={open}
            className={cn(
              "min-h-11 w-full justify-between px-3 font-normal",
              hideLabel ? "mt-0" : "mt-2",
            )}
          >
            <span
              className={cn("truncate", !selected && "text-muted-foreground")}
            >
              {selected?.label ?? placeholder ?? "—"}
            </span>
            <ChevronsUpDown className="size-4 shrink-0 opacity-55" />
          </Button>
        </PopoverTrigger>
        <PopoverContent
          align="start"
          className="w-[var(--radix-popover-trigger-width)] min-w-[220px] p-0"
        >
          <Command>
            <CommandInput placeholder="بحث… / Search…" />
            <CommandList>
              <CommandEmpty>لا توجد نتائج / No results</CommandEmpty>
              <CommandGroup>
                {options.map((option) => (
                  <CommandItem
                    key={option.value}
                    value={`${option.label} ${option.value}`}
                    disabled={option.disabled}
                    onSelect={() => {
                      onChange(option.value);
                      setOpen(false);
                    }}
                  >
                    <Check
                      className={cn(
                        "size-4",
                        option.value === value ? "opacity-100" : "opacity-0",
                      )}
                    />
                    <span className="truncate">{option.label}</span>
                  </CommandItem>
                ))}
              </CommandGroup>
            </CommandList>
          </Command>
        </PopoverContent>
      </Popover>
    </div>
  );
}
