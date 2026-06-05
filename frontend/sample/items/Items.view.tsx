import { View } from "react-native";
import { useTranslation } from "react-i18next";
import { Resource } from "@lazuli/react";
// Illustrative: the app's design-system primitives + a thin Screen wrapper.
import { Screen, Text, EmptyState } from "@/ui";
import { useItemsModel } from "./Items.viewModel";
import type { Item } from "./Items.viewModel";

// CANONICAL VIEW — render only (LZFE001). It consumes the resource through <Resource>, so loading / error / empty
// are handled by construction and the list body only ever runs with resolved data. No isPending/isError here.
export function ItemsView() {
  const { t } = useTranslation("items");
  const { state } = useItemsModel();

  return (
    <Resource
      state={state.items}
      empty={
        <Screen>
          <EmptyState title={t("empty.title")} description={t("empty.description")} />
        </Screen>
      }
    >
      {(items) => <ItemList items={items} />}
    </Resource>
  );
}

function ItemList({ items }: { items: Item[] }) {
  return (
    <Screen>
      <View className="gap-3">
        {items.map((item) => (
          <Text key={item.id} variant="body">
            {item.name}
          </Text>
        ))}
      </View>
    </Screen>
  );
}
