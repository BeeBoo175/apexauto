import { isPositiveInteger, isUuid } from "@/lib/auth/contracts";
import { shapeDocumentCorrection, exactKeys } from "@/lib/sales/completion-input";
import { parseDocumentCorrection } from "@/lib/sales/documents";
import { relaySaleCommand } from "@/lib/sales/routes";

export async function POST(request: Request, context: { params: Promise<{ id: string }> }) {
  const { id } = await context.params;
  return relaySaleCommand(
    request,
    `/${id}/document-corrections`,
    (body) => {
      if (
        !isUuid(body.operationId) ||
        !isPositiveInteger(body.expectedVersion) ||
        !exactKeys(body, [
          "operationId",
          "expectedVersion",
          "documentId",
          "buyerName",
          "buyerAddress",
          "reason",
        ])
      ) {
        return null;
      }
      const fields = shapeDocumentCorrection(body);
      return fields
        ? { operationId: body.operationId, expectedVersion: body.expectedVersion, ...fields }
        : null;
    },
    parseDocumentCorrection,
  );
}
