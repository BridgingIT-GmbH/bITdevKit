# ShopFiesta Wireframe Prompts

These raster concepts accompany [the ShopFiesta application specification](../../spec-shopfiesta.md). They were generated and revised with the built-in `image_gen` tool. They are design artifacts, not runtime screenshots or evidence that the application/framework has been implemented.

## Shared direction

Use one readable landscape desktop wireframe per screen, white backgrounds, grayscale panels, thin hand-drawn black outlines, light hatching and crisp dark text. Avoid color, photography, decorative gradients, shadows and implementation jargon in customer flows. Keep each screen independent rather than combining a contact sheet.

The name is ShopFiesta, with a product-neutral wordmark and no outdoor logo/slogan. The initial catalog is road/race and gravel bicycle components only. No clothing, complete bicycles, trail products or general-purpose/computer hardware. Product/category/attribute choices are initial fixtures, not fixed application or shell concepts. Customer navigation uses Drivetrain, Brakes and Wheels, product search, Account and Basket (3). My orders appears in the signed-in account context.

Manufacturer model designations are optional in the catalog schema but must accompany every displayed product name when populated. Use an inline label such as Gravel Brake Rotor (RT-G800), or a readable Model: RT-G800 line directly beneath the name. Apply this to product cards/headings/breadcrumbs, basket and checkout lines, purchased order lines, recommendation source/results/preview and reporting selectors. Compact layouts retain the code instead of hiding it in a tooltip. Fictional Aster codes are Road Brake Rotor `RT-R600`, Gravel Brake Rotor `RT-G800`, Road Brake Caliper `BR-R950`, Rotor Lockring `LR-150` and Brake Adapter `AD-200`. These illustrate technical naming without turning the fixtures into Shimano products. Brand, model, selected variant and merchant SKU have separate meanings.

## Catalog

Asset: [catalog.png](catalog.png).

Show category Brakes, breadcrumb Home > Brakes, brand Aster selected, Orbit unselected, EUR 50 to EUR 100, In stock and applied filters. Three products sorted by price: Road Brake Rotor EUR 60, Gravel Brake Rotor EUR 80 and Road Brake Caliper EUR 95. Draw isolated components, not complete bicycles. Each card has View product and Add to basket. Show three products and page 1 with unavailable Previous/Next.

The address is `/catalog/brakes?brand=aster&min=50&max=100&stock=in&sort=price-asc&page=1`. It must agree with the selected filters and displayed order.

## Product

Asset: [product.png](product.png).

Show Gravel Brake Rotor by Aster at EUR 80, In stock, quantity 1 and Add to basket beside an isolated rotor sketch. Breadcrumb Home > Brakes > Gravel Brake Rotor. Sample attributes are Diameter: 160 mm and Mount: center-lock. Related products contains Rotor Lockring EUR 15 and Brake Adapter EUR 20 with isolated component sketches and separate View product/Add to basket controls. The specification declares `/products/gravel-brake-rotor` as the canonical product URL.

The model-designation edit adds Model: RT-G800 below Aster, Model: LR-150 beneath Rotor Lockring and Model: AD-200 beneath Brake Adapter. Preserve all names, amounts, quantity, attributes, actions, navigation and monochrome sketch styling. These are readable display labels; no ID, URL, purchase target or underlying product is changed by the label addition. Preserve the original generated source outside the documentation tree.

The content-delivery edit adds one underlined Installation manual (PDF) link below Mount: center-lock, keeping it above Related products. Preserve all model codes, amounts and controls. The eventual backend resolves the image through DevKit Blob Storage and the manual through DevKit File Storage. The frontend-first slice can use Catalog-owned fixture content URLs with the same visible contract. The customer screen displays ordinary product content; do not add provider names, raw paths, queueing labels or infrastructure instructions. The image itself is a concept, not proof of provider-backed delivery.

The identifier-panel edit adds a secondary Product identifiers panel for the selected variant: SKU AST-RTG800-160-CL, Manufacturer part number RT-G800-160-CL and GTIN Not provided. Grow the canvas vertically rather than cover or shrink existing controls. Do not invent an allocated GTIN or barcode. Preserve all three existing model designations, prices, quantity, stock label and manual link. The running frontend receives current availability from Stock separately from these Catalog identifiers.

## Basket

Asset: [basket.png](basket.png).

Show Gravel Brake Rotor at EUR 80, quantity 2 and line total EUR 160. Rotor Lockring is EUR 15, quantity 1 and line total EUR 15. Both lines have quantity controls and Remove. Summary has subtotal EUR 175, delivery EUR 5 and total EUR 180. Basket (3) means three units. Include Continue to checkout, Continue shopping and illustrative-prices wording. No payment fields.

## Checkout

Asset: [checkout.png](checkout.png).

Show native contact/delivery form concepts without actual personal data. The empty postcode has Enter a postcode feedback and the summary says Check your delivery details. The review is Gravel Brake Rotor x2 EUR 160, Rotor Lockring x1 EUR 15, delivery EUR 5 and total EUR 180. Place order - EUR 180 is disabled in this invalid state. Include Back to basket and Demo checkout - no real payment. No credentials or claimed success.

## Customer history

Asset: [customer-orders.png](customer-orders.png).

Show the signed-in account's My orders page at `/account/orders`, with Home > Account > My orders breadcrumbs. Rows show SF-1042, 12 Nov 2026, Processing, EUR 180; SF-0987, 24 Oct 2026, Delivered, EUR 95; and SF-0902, 04 Sep 2026, Delivered, EUR 60. Each has View order. Use bounded paging and Continue shopping. Do not add customer selection, row checkboxes, Approve, Release or staff data. The page represents only the current customer's purchases.

## Customer detail

Asset: [customer-order-detail.png](customer-order-detail.png).

Show Order SF-1042, Ordered 12 November 2026, Processing, and a customer-visible progress row with Placed complete, Processing current, Shipped/Delivered later. Breadcrumb Home > Account > My orders > SF-1042. Detail uses the purchased snapshot: two Gravel Brake Rotors at EUR 80, line total EUR 160; one Rotor Lockring at EUR 15, line total EUR 15; subtotal EUR 175, delivery EUR 5, total EUR 180. Include Back to my orders and Continue shopping. No staff commands, internal review data or personal/payment credentials. The declared route uses an actual order GUID, such as `/account/orders/11111111-1111-4111-8111-111111111042`, rather than the display order number.

## Operations

Asset: [operations.png](operations.png).

Use ShopFiesta | Operations and Staff account with `/operations/orders`. Two independent queue tables are Ready for fulfillment and Needs review. The external toolbar explicitly targets Ready for fulfillment, says 2 selected and offers Release selected (2) plus Refresh. Include friendly status/search controls.

Ready has SF-1042 Paid EUR 180 and SF-1043 Paid EUR 95 selected, plus SF-1044 Paid EUR 60 unselected. Review has SF-2090 Review EUR 80 selected and SF-2091 Review EUR 35 unselected, with local Open review. Both table-header boxes show an indeterminate dash. No customer personal data or debug labels.

The updated external toolbar is one bordered two-row group. The first row identifies Ready, its two selected targets and Release selected, Refresh and Export matching. The second row contains Status: Paid and Search orders inside that same border. Do not leave a duplicate filter row outside the header. Query controls and actions share the explicit Ready source; Review stays independent.

## Preferences

Asset: [preferences.png](preferences.png).

Show `/preferences` with the normal customer header, Home > Preferences and two native-form sections. Appearance and language contains Theme System and Language English (UK). Order display contains Orders per page 25 and Compact rows checked. Each section has its own save and feedback: Save appearance / Appearance saved, and Save order display / Order display saved. A targeted edit removed the original page-wide Save preferences control because composing module sections does not introduce Save All or a cross-module transaction. No private checkout data or staff controls.

## Order export status

Asset: [order-export.png](order-export.png).

Use staff chrome and breadcrumb Orders > Exports > EX-1042. Show Order export, EX-1042 and Operation: Running. A source-reported progress bar is approximately two-thirds filled and says 2 of 3 orders prepared. The captured query summary says Filter status: Paid and Format: CSV. These labels deliberately distinguish the operation state from the exported order-query filter.

Include Refresh status, Back to orders, and a disabled Download CSV with Available when complete. State that the page can be left and revisited. No Cancel action, false success, personal data or secret artifact URL. The specification identifies the initial backend source as simulated; static artwork is not evidence of a real queue, durable job or completed export.

## Commerce overview and product funnel

Asset: [commerce-overview.png](commerce-overview.png). Spec: [staff reporting](../../spec-shopfiesta.md#compose-staff-business-metrics-and-a-product-funnel).

Use the approved Operations sketch as the style reference for a new staff Commerce overview screen. Keep ShopFiesta | Operations, Staff account, Overview active and Orders/Marketing sidebar entries. Show From 12 Nov 2026 00:00 UTC, To 12 Nov 2026 12:00 UTC, Currency EUR and Refresh. Preserve legible black outlines, white/gray panels and light hatching. Grow the canvas vertically to keep the added funnel readable.

Orders and sales shows Orders placed 5, Paid orders 3, Paid order value EUR 335 and Average paid order EUR 111.67. Checkout submissions shows Submitted 7, Confirmed 5, Rejected 1 and Pending/unknown 1, with Submission success 5 / 7 = 71.4%. These are fictional business fixtures, not runtime/performance measurements.

Marketing owns this page and every reporting panel. All values and the reporting product selector come from Marketing's stored projections, captured from Catalog, Basket, Checkout and Orders integration messages through DevKit Messaging. The panel headings name the measures; they do not imply that the page queries those modules. Interpret the as-of footer as Marketing projection freshness. The frontend-first slice supplies these values through a Marketing fixture handler. Actual message capture and stored projections remain separate backend integration work. The wireframe establishes neither a live source query nor durable delivery.

The initial generated overview was edited with the image tool to add Product funnel below Checkout submissions. Product is Gravel Brake Rotor. Label the denominator Distinct shopping journeys - view cohort. Show Viewed 20, Added to basket 8, Submitted checkout 6 and Bought (paid) 3 with arrows between them. Show View to basket 40% and View to bought 15%. Retain the aggregate values above and the footer Fixture data - As of 12 Nov 2026 12:00 UTC. The spec defines the 24-hour observation rule and identifies this still-maturing cohort; the drawing does not establish a complete lifetime conversion rate.

Do not add a made-up time series, technical telemetry, customer personal data, release/save controls or a claim that raw clicks are distinct shoppers. The original generation sources remain preserved outside the documentation tree. The selected concept is the funnel revision.

## Marketing recommendation management

Asset: [marketing-recommendations.png](marketing-recommendations.png). Spec: [Marketing management](../../spec-shopfiesta.md#manage-recommendations-in-the-marketing-module).

Generate a new staff editor using the approved Operations sketch for its monochrome style and staff chrome. Marketing is active in the sidebar. Breadcrumb Marketing > Product recommendations, heading Product recommendations. Source product Gravel Brake Rotor is read-only with Choose another product. Enabled for storefront is checked.

Recommended products contains exactly two ordered rows: Rotor Lockring EUR 15 with Move down/Remove, and Brake Adapter EUR 20 with Move up/Remove. Find components has Catalog search results Road Brake Rotor EUR 60 and Road Brake Caliper EUR 95, each with Add. A separate Related products preview shows the draft names/prices, with no Add to basket or purchasing controls. Include Save recommendations and Changes appear after saving. The Live badge indicates the existing published set, not that unsaved edits have been published.

Keep versions, GUIDs, binding/debug terminology, personal information and technical instructions out of the normal editor. Illustrate bike components only. This is a proposed native-form workflow and not evidence of implemented recommendation persistence or conflict handling.

## Core Orders illustration style pass

The following existing specification images use the same approved monochrome sketch language:

- [Core backoffice shell](../backoffice-shell.png).
- [External toolbar binding](../external-toolbar-binding.png).
- [Typed scopes and placement](../typed-scopes-and-placement.png).
- [Orders list](../orders-list-toolbar.png).
- [Direct order detail](../order-detail-deep-link.png).

Each original core image was an edit target; the approved shop operations sketch supplied the style reference. Preserve original routes, brand/labels, record identifiers, action targets, prices, selection and disabled states. Convert blue/polished styling to black/white outlines and subtle hatching. Preserve checked headers when all visible rows are selected and use an indeterminate dash for partial selection. Targeted follow-up edits corrected the Orders B header boxes without altering row selection.

## Product-label consistency revision

Use the supplied basket image as the basket edit target and each existing local image as its own edit target. Add adjacent technical model text with the built-in image tool, preserving the original generated sources outside the documentation tree. Keep the established monochrome style, layout, product illustrations, controls/states, prices, quantities and metrics. Use a second line in narrow rows rather than overlap prices or excessively shrink text.

- [Basket](basket.png): Gravel Brake Rotor (RT-G800) and Rotor Lockring (LR-150). Keep quantities 2/1, line totals EUR 160/15, subtotal EUR 175, delivery EUR 5 and total EUR 180.
- [Checkout](checkout.png): the same two names/codes in its order summary. Keep the field errors and disabled Place order control.
- [Customer order detail](customer-order-detail.png): both purchased lines retain their own recorded codes and existing quantities/prices. Keep SF-1042 Processing and the customer-only actions.
- [Catalog](catalog.png): RT-R600, RT-G800 and BR-R950 under the corresponding three product names. Preserve the filter/query/sort/page state.
- [Product detail](product.png): retain its existing three model rows and manual link; add RT-G800 to the product breadcrumb label too.
- [Marketing editor](marketing-recommendations.png): add codes to the source field, both recommendation rows, both draft-preview names and both lookup candidates. Preview stays read-only.
- [Commerce overview](commerce-overview.png): the Product selector shows Gravel Brake Rotor (RT-G800). Preserve every metric and the period/as-of labels.

Order-history, operations, preferences and export-status images do not display individual product names and need no model-label insertion.

## Stock management

Asset: [stock.png](stock.png). Spec: [Stock presentation and management](../../spec-shopfiesta.md#compose-stock-availability-and-manage-ordering-policy).

Generate one readable monochrome staff Stock availability page with Operations > Stock, Stock active in the sidebar, a SKU/model search and a native SKU policy editor. Product names include their technical designations. Catalog owns the product identities; Stock uses the exact same SKU strings. This is a separate stock-change scenario, not a replacement of the earlier all-in-stock Catalog fixture.

The table has SKU, Product, Available quantity, Availability and Can order. Rows are AST-RTG800-160-CL / Gravel Brake Rotor (RT-G800) / 12 / In stock / Yes; AST-RTR600-160-CL / Road Brake Rotor (RT-R600) / 3 / Low stock / Yes; AST-BRR950-FRONT / Road Brake Caliper (BR-R950) / 0 / Sold out / No; AST-LR150-EA / Rotor Lockring (LR-150) / 0 / Coming soon / Yes, Preorder allowed; and AST-AD200-EA / Brake Adapter (AD-200) / no known quantity / Available to order / Yes.

Select AST-AD200-EA. The right panel shows Quantity empty with Quantity not known, Allow orders when quantity is unknown checked, Allow backorders unchecked, Low-stock threshold 5, Minimum available to accept orders 1 and Save stock settings. Targeted edits corrected labels that conflated reported/on-hand quantity and a display-only minimum; the final quantity label is the simple Quantity. Preserve the distinction between low-stock display threshold and minimum quantity for accepting orders. The explicit unknown-quantity allowance permits this SKU despite its empty quantity. Footer Illustrative stock-change scenario. No prices, private data, raw GTINs or backend implementation instructions.

## Review invariants

- ShopFiesta frontend/backend business modules correspond under the same logical identities.
- Initial shop imagery contains only the declared bike-component fixture catalog, while application contracts remain product-agnostic.
- Customer order history/detail and staff operations are different authorized facets of the same Orders module.
- Filter values, product ordering and catalog query values agree.
- Basket quantities, line totals, badge and checkout/customer-order totals agree.
- A customer sees no staff Release/Approve controls or other customers' records.
- The operations header targets only selected Ready orders.
- Images explain a specification; minimal/mocked backend and future full DevKit-showcase work are not runtime evidence.
