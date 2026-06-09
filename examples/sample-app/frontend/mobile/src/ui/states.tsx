import { Button } from "./Button";
import { Stack } from "./Stack";
import { Text } from "./Text";

export function EmptyState({ title, description }: { title: string; description?: string }) {
  return (
    <Stack gap="xs" align="center" padding="xl">
      <Text role="heading">{title}</Text>
      {description ? <Text tone="muted">{description}</Text> : null}
    </Stack>
  );
}

export function ErrorState({
  title,
  retryLabel,
  onRetry,
}: {
  title: string;
  retryLabel?: string;
  onRetry?: () => void;
}) {
  return (
    <Stack gap="sm" align="center" padding="xl">
      <Text role="heading" tone="danger">
        {title}
      </Text>
      {onRetry && retryLabel ? <Button label={retryLabel} onPress={onRetry} variant="secondary" /> : null}
    </Stack>
  );
}
