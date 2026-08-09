import { NodeSDK } from '@opentelemetry/sdk-node';
import { getNodeAutoInstrumentations } from '@opentelemetry/auto-instrumentations-node';
import { OTLPTraceExporter } from '@opentelemetry/exporter-trace-otlp-http';

// No-op when the endpoint is unset, so the app runs normally with the
// observability stack stopped.
const endpoint = process.env.OTEL_EXPORTER_OTLP_ENDPOINT;

export const sdk = endpoint
  ? new NodeSDK({
      traceExporter: new OTLPTraceExporter({ url: `${endpoint}/v1/traces` }),
      instrumentations: [getNodeAutoInstrumentations()],
    })
  : undefined;

if (sdk) {
  sdk.start();
  process.on('SIGTERM', () => {
    sdk.shutdown().finally(() => process.exit(0));
  });
}
