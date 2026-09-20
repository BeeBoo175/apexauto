// One numbered section of a legal document: a heading, an opening paragraph or two, an optional list, and any
// paragraphs that follow the list.
export interface LegalSection {
  heading: string;
  paragraphs: readonly string[];
  items?: readonly string[];
  after?: readonly string[];
}
