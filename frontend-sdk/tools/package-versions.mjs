// Published frontend packages that every AeroFortress pilot with a frontend must consume together.
// This file ships inside @aerofortress/frontend-sdk, making the sync check independent of a sibling
// framework checkout and therefore effective in CI as well as on a developer machine.
export const FRONTEND_PACKAGE_VERSIONS = Object.freeze([
  Object.freeze({ name: "@aerofortress/frontend-sdk", version: "1.0.0" }),
  Object.freeze({ name: "@aerofortress/react", version: "1.0.0" }),
  Object.freeze({ name: "eslint-plugin-aerofortress", version: "1.0.0" }),
]);
