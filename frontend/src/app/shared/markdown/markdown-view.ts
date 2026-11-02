import { NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { parseMarkdown } from './markdown';

/** Shows Markdown text as headings, paragraphs and lists (see markdown.ts); places still to be checked are highlighted. */
@Component({
  selector: 'app-markdown-view',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NgTemplateOutlet],
  templateUrl: './markdown-view.html',
  styleUrl: './markdown-view.scss',
})
export class MarkdownView {
  readonly source = input.required<string>();

  protected readonly blocks = computed(() => parseMarkdown(this.source()));

  protected isExternal(href: string): boolean {
    return /^https?:/iu.test(href);
  }
}
