export type PluralCategory = 'one' | 'few' | 'many' | 'other'

const rules: Record<'en' | 'ru', Intl.PluralRules> = {
  en: new Intl.PluralRules('en'),
  ru: new Intl.PluralRules('ru'),
}

export function selectPlural(lang: 'en' | 'ru', count: number): PluralCategory {
  return rules[lang].select(count) as PluralCategory
}
