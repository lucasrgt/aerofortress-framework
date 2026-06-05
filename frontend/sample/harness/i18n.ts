import i18next from "i18next";
import { initReactI18next } from "react-i18next";
import { enUS, esES, ptBR } from "../items/items.i18n";

// Harness i18n instance (`@/i18n`) wired with the sample feature's catalog, so the ViewModel's
// `i18n.t("items:error")` resolves. A real lazuli-net app assembles this resource tree from every feature's
// `*.i18n.ts` (the generator the roadmap calls "i18n assembly"); here we wire the one sample feature by hand.
void i18next.use(initReactI18next).init({
  lng: "en",
  fallbackLng: "en",
  resources: {
    en: { items: enUS },
    pt: { items: ptBR },
    es: { items: esES },
  },
  interpolation: { escapeValue: false },
});

export default i18next;
