# Modal Component

We need a reusable Razor component that allows us to show dynamic content in a modal that pops up in the middle of the screen. 

## States

The modal will have two states:
- Closed (default)
- Opened

When it is closed, it shows the "ClosedContent" render fragment below. The render fragment has a callback mechanism that changes the stated from "Closed" to "Open" when clicked. This hides ClosedConent and shows HeaderContent, MainContent, and FooterContent stacked in a popup. Then if closed, the reverse happens: the modal is hidden and ClosedContent is rendered.

## Events

The modal will have five events the caller can subscribe to:
1. Opening: fires when the ClosedContent callback is invoked
2. Opened: fires when the opened modal is fully rendered.
3. Affirmed: fires when the user invokes the FooterContent's OK callback
4. Canceled: fires when the user invokes the FooterContent's Cancel callback
5. Closed: fires when ClosedContent is re-rendered after either events 3 or 4 above.

## Content

The modal will host four content areas as render fragments:
1. ClosedContent: if not set by the caller, shows a button labeled with the ButtonText parameter
1. HeaderContent: if not set by the caller, shows the Title parameter
2. MainContent: must be set by the caller
3. FooterContent: if not set by the caller, shows two centered buttons that each invoke one of two callbacks
  - Ok: raises a blocking callback "OnOk" event and then performs a 
  - Cancel: closes the modal

## Parameters

The modal will have the following parameters:
- Title (string): a header to show at the top of the modal.
- Width (double): a value between 0 and 1 that dictates which percentage of the screen width the modal should occupy.
- AutoClose (boolean): if true, clicking outside the modal closes it with no further interaction; on the FooterContent can close the modal.