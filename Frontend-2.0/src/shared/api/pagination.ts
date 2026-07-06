// Envelope returned by paged admin list endpoints (jobs / artifacts /
// agents / audit log family). Requesting any of page/pageSize/sortBy
// switches the backend from the legacy full-array shape to this one.
export interface PagedResponse<T> {
  items: T[]
  total: number
  page: number
  pageSize: number
}

export type SortDir = 'asc' | 'desc'
