import { Catalog, Category } from './catalog.models';

function moved<T>(items: readonly T[], from: number, to: number): T[] {
  if (from === to || from < 0 || to < 0 || from >= items.length || to >= items.length) {
    return [...items];
  }
  const copy = [...items];
  const [item] = copy.splice(from, 1);
  copy.splice(to, 0, item);
  return copy;
}

const renumbered = <T extends { sortOrder: number }>(items: T[]): T[] =>
  items.map((item, index) => ({ ...item, sortOrder: index }));

export function moveCategory(catalog: Catalog, from: number, to: number): Catalog {
  return { categories: renumbered(moved(catalog.categories, from, to)) };
}

export function moveService(
  catalog: Catalog,
  categoryId: string,
  from: number,
  to: number,
): Catalog {
  return {
    categories: catalog.categories.map((category): Category =>
      category.id === categoryId
        ? { ...category, services: renumbered(moved(category.services, from, to)) }
        : category,
    ),
  };
}

export const categoryIds = (catalog: Catalog): string[] => catalog.categories.map((c) => c.id);

export const serviceIds = (catalog: Catalog, categoryId: string): string[] =>
  catalog.categories.find((c) => c.id === categoryId)?.services.map((s) => s.id) ?? [];
