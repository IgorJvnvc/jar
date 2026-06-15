import { request } from '../lib/http'
import type {
  AddPoolHallRequest,
  AddPoolHallTableRequest,
  PoolHallDetailResponse,
  PoolHallResponse,
  PoolHallTableResponse,
  RatePoolHallRequest,
  RatePoolHallTableRequest,
} from '../lib/types'

export const hallsApi = {
  list(accessToken: string): Promise<PoolHallResponse[]> {
    return request<PoolHallResponse[]>('/api/halls', {
      accessToken,
    })
  },

  getById(accessToken: string, hallId: string): Promise<PoolHallDetailResponse> {
    return request<PoolHallDetailResponse>(`/api/halls/${hallId}`, {
      accessToken,
    })
  },

  add(accessToken: string, payload: AddPoolHallRequest): Promise<PoolHallResponse> {
    return request<PoolHallResponse>('/api/halls', {
      method: 'POST',
      accessToken,
      body: payload,
    })
  },

  addTable(accessToken: string, hallId: string, payload: AddPoolHallTableRequest): Promise<PoolHallTableResponse> {
    return request<PoolHallTableResponse>(`/api/halls/${hallId}/tables`, {
      method: 'POST',
      accessToken,
      body: payload,
    })
  },

  rateHall(accessToken: string, hallId: string, payload: RatePoolHallRequest): Promise<void> {
    return request<void>(`/api/halls/${hallId}/ratings`, {
      method: 'POST',
      accessToken,
      body: payload,
    })
  },

  rateTable(accessToken: string, tableId: string, payload: RatePoolHallTableRequest): Promise<void> {
    return request<void>(`/api/halls/tables/${tableId}/ratings`, {
      method: 'POST',
      accessToken,
      body: payload,
    })
  },
}
