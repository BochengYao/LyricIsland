import type {
  TsvParsedRow,
  TsvParseResult,
  TsvParseError,
  TsvImportPreview,
} from "../data/promo-code-types";

// ── Constants ────────────────────────────────────────────────────────────────

const MAX_FILE_SIZE = 5 * 1024 * 1024; // 5 MB in characters
const MAX_ROWS = 50_000;

const INJECTION_PREFIXES = ["=", "+", "-", "@"] as const;

// ── Header alias maps ───────────────────────────────────────────────────────

type CanonicalField =
  | "code"
  | "redeem_url"
  | "microsoft_code_id"
  | "raw_order_id"
  | "order_name"
  | "given_to"
  | "microsoft_available"
  | "microsoft_redeemed"
  | "microsoft_start_at"
  | "microsoft_expire_at";

const HEADER_ALIASES: Record<string, CanonicalField> = {
  "promotional code": "code",
  "promo code": "code",
  code: "code",
  "redeemable url": "redeem_url",
  "redeem url": "redeem_url",
  "redemption url": "redeem_url",
  "code id": "microsoft_code_id",
  codeid: "microsoft_code_id",
  "order id": "raw_order_id",
  orderid: "raw_order_id",
  "order name": "order_name",
  ordername: "order_name",
  "given to": "given_to",
  available: "microsoft_available",
  redeemed: "microsoft_redeemed",
  "start date": "microsoft_start_at",
  startdate: "microsoft_start_at",
  "expiration date": "microsoft_expire_at",
  expirationdate: "microsoft_expire_at",
  "expiry date": "microsoft_expire_at",
};

// ── Helpers ──────────────────────────────────────────────────────────────────

function parseBoolean(value: string): boolean | null {
  const v = value.trim().toLowerCase();
  if (v === "yes" || v === "true" || v === "1") return true;
  if (v === "no" || v === "false" || v === "0") return false;
  return null;
}

function parseDate(
  value: string,
  rowNumber: number,
  fieldName: string,
  errors: TsvParseError[]
): string | null {
  const trimmed = value.trim();
  if (trimmed === "") return null;

  // Try ISO: YYYY-MM-DD or YYYY-MM-DDTHH:MM:SS
  const isoMatch = trimmed.match(
    /^(\d{4})-(\d{2})-(\d{2})(?:T(\d{2}):(\d{2}):(\d{2}))?$/
  );
  if (isoMatch) {
    const d = new Date(trimmed);
    if (!isNaN(d.getTime())) return trimmed;
  }

  // Try US format: MM/DD/YYYY
  const usMatch = trimmed.match(/^(\d{1,2})\/(\d{1,2})\/(\d{4})$/);
  if (usMatch) {
    const month = usMatch[1].padStart(2, "0");
    const day = usMatch[2].padStart(2, "0");
    const year = usMatch[3];
    const iso = `${year}-${month}-${day}`;
    const d = new Date(iso);
    if (!isNaN(d.getTime())) return iso;
  }

  errors.push({
    row: rowNumber,
    message: `Invalid date in "${fieldName}": "${trimmed}"`,
  });
  return null;
}

function detectInjection(cells: string[]): boolean {
  return cells.some((cell) => {
    const trimmed = cell.trimStart();
    return INJECTION_PREFIXES.some((prefix) => trimmed.startsWith(prefix));
  });
}

// ── Core parser ──────────────────────────────────────────────────────────────

export function parseTsv(text: string): TsvParseResult {
  const errors: TsvParseError[] = [];
  const warnings: string[] = [];

  // Size limit
  if (text.length > MAX_FILE_SIZE) {
    return {
      rows: [],
      errors: [{ row: 0, message: `File too large: ${text.length} characters exceeds ${MAX_FILE_SIZE} limit` }],
      warnings: [],
      total_lines: 0,
    };
  }

  // 1. Strip BOM (UTF-8 BOM or residual UTF-16 BOM)
  let cleaned = text.startsWith("\uFEFF") ? text.slice(1) : text;
  cleaned = cleaned.startsWith("\uFFFE") ? cleaned.slice(1) : cleaned;

  // 2. Strip null bytes (residual from UTF-16 decoding)
  cleaned = cleaned.replace(/\0/g, "");

  // 3. Normalize line endings
  cleaned = cleaned.replace(/\r\n/g, "\n").replace(/\r/g, "\n");

  // 4. Split into lines, track original line numbers
  const rawLines = cleaned.split("\n");

  // Build array of { lineNumber, content } skipping empty lines
  type NumberedLine = { lineNumber: number; content: string };
  const numberedLines: NumberedLine[] = [];
  for (let i = 0; i < rawLines.length; i++) {
    if (rawLines[i].trim() !== "") {
      numberedLines.push({ lineNumber: i + 1, content: rawLines[i] });
    }
  }

  if (numberedLines.length === 0) {
    return {
      rows: [],
      errors: [{ row: 0, message: "No header row found — file is empty" }],
      warnings: [],
      total_lines: 0,
    };
  }

  // 5. Parse header row — identify columns by name
  const headerLine = numberedLines[0];
  const headerCells = headerLine.content.split("\t").map((h) => h.trim().toLowerCase());

  const columnMap = new Map<CanonicalField, number>();
  for (let i = 0; i < headerCells.length; i++) {
    const canonical = HEADER_ALIASES[headerCells[i]];
    if (canonical !== undefined && !columnMap.has(canonical)) {
      columnMap.set(canonical, i);
    }
  }

  // Validate required columns
  if (!columnMap.has("microsoft_code_id")) {
    return {
      rows: [],
      errors: [
        {
          row: headerLine.lineNumber,
          message:
            'Required column "Code ID" (or "CodeID") not found in header. Found headers: ' +
            headerCells.join(", "),
        },
      ],
      warnings: [],
      total_lines: numberedLines.length,
    };
  }

  if (!columnMap.has("code")) {
    return {
      rows: [],
      errors: [
        {
          row: headerLine.lineNumber,
          message:
            'Required column "Promotional code" (or "Promo Code" / "Code") not found in header. Found headers: ' +
            headerCells.join(", "),
        },
      ],
      warnings: [],
      total_lines: numberedLines.length,
    };
  }

  // Helper to safely get cell value
  const getCell = (cells: string[], field: CanonicalField): string => {
    const idx = columnMap.get(field);
    if (idx === undefined || idx >= cells.length) return "";
    return cells[idx].trim();
  };

  // 6. Parse data rows
  const rows: TsvParsedRow[] = [];
  const dataLines = numberedLines.slice(1);

  if (dataLines.length > MAX_ROWS) {
    return {
      rows: [],
      errors: [
        {
          row: 0,
          message: `Too many rows: ${dataLines.length} exceeds maximum of ${MAX_ROWS}`,
        },
      ],
      warnings: [],
      total_lines: numberedLines.length,
    };
  }

  for (const line of dataLines) {
    const cells = line.content.split("\t");
    const rowNumber = line.lineNumber;

    // Formula injection detection
    const hasInjectionRisk = detectInjection(cells);
    if (hasInjectionRisk) {
      warnings.push(
        `Row ${rowNumber}: contains a cell starting with =, +, -, or @ — potential formula injection risk`
      );
    }

    const microsoftCodeId = getCell(cells, "microsoft_code_id");
    const codeValue = getCell(cells, "code");

    // Row-level validation: microsoft_code_id required
    if (microsoftCodeId === "") {
      errors.push({
        row: rowNumber,
        message: "Missing required field: microsoft_code_id (Code ID)",
      });
      continue; // skip row
    }

    // code required
    if (codeValue === "") {
      errors.push({
        row: rowNumber,
        message: "Missing required field: code (Promotional code)",
      });
      continue;
    }

    const row: TsvParsedRow = {
      microsoft_code_id: microsoftCodeId,
      code: codeValue,
      redeem_url: getCell(cells, "redeem_url") || null,
      raw_order_id: getCell(cells, "raw_order_id") || null,
      order_name: getCell(cells, "order_name") || null,
      given_to: getCell(cells, "given_to") || null,
      microsoft_available: parseBoolean(getCell(cells, "microsoft_available")),
      microsoft_redeemed: parseBoolean(getCell(cells, "microsoft_redeemed")),
      microsoft_start_at: parseDate(
        getCell(cells, "microsoft_start_at"),
        rowNumber,
        "Start date",
        errors
      ),
      microsoft_expire_at: parseDate(
        getCell(cells, "microsoft_expire_at"),
        rowNumber,
        "Expiration date",
        errors
      ),
      has_injection_risk: hasInjectionRisk,
      row_number: rowNumber,
    };

    rows.push(row);
  }

  return {
    rows,
    errors,
    warnings,
    total_lines: numberedLines.length,
  };
}

// ── File reader ──────────────────────────────────────────────────────────────

export function parseTsvFile(file: File): Promise<TsvParseResult> {
  return new Promise((resolve) => {
    const reader = new FileReader();
    reader.onload = () => {
      try {
        const buffer = reader.result as ArrayBuffer;
        const bytes = new Uint8Array(buffer);
        let text: string;

        if (bytes.length >= 2 && bytes[0] === 0xff && bytes[1] === 0xfe) {
          // UTF-16 LE BOM
          text = new TextDecoder("utf-16le").decode(buffer);
        } else if (bytes.length >= 2 && bytes[0] === 0xfe && bytes[1] === 0xff) {
          // UTF-16 BE BOM
          text = new TextDecoder("utf-16be").decode(buffer);
        } else if (
          bytes.length >= 3 &&
          bytes[0] === 0xef &&
          bytes[1] === 0xbb &&
          bytes[2] === 0xbf
        ) {
          // UTF-8 with BOM
          text = new TextDecoder("utf-8").decode(buffer);
        } else {
          // Default to UTF-8
          text = new TextDecoder("utf-8").decode(buffer);
        }

        resolve(parseTsv(text));
      } catch {
        resolve({
          rows: [],
          errors: [{ row: 0, message: `Failed to decode file: ${file.name}` }],
          warnings: [],
          total_lines: 0,
        });
      }
    };
    reader.onerror = () => {
      resolve({
        rows: [],
        errors: [{ row: 0, message: `Failed to read file: ${file.name}` }],
        warnings: [],
        total_lines: 0,
      });
    };
    reader.readAsArrayBuffer(file);
  });
}

// ── Import preview ───────────────────────────────────────────────────────────

export function generateImportPreview(
  filename: string,
  parseResult: TsvParseResult
): TsvImportPreview {
  return {
    filename,
    total_detected: parseResult.rows.length,
    new_count: parseResult.rows.length,
    existing_count: 0,
    microsoft_status_changes: 0,
    unchanged_count: 0,
    errors: parseResult.errors,
    warnings: parseResult.warnings,
  };
}
