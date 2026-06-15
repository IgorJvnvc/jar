import type { CueRarity, DuelStatus } from './types'

export const cueRarityLabel = (rarity: CueRarity): string => {
  switch (rarity) {
    case 'Common':
      return 'Common'
    case 'Rare':
      return 'Rare'
    case 'Epic':
      return 'Epic'
    case 'Legendary':
      return 'Legendary'
    default:
      return rarity
  }
}

export const duelStatusLabel = (status: DuelStatus): string => {
  switch (status) {
    case 'Pending':
      return 'Pending'
    case 'Declined':
      return 'Declined'
    case 'Accepted':
      return 'Accepted'
    case 'AwaitingSecondReview':
      return 'Second Review'
    case 'CoinFlipInProgress':
      return 'Coin Flip'
    case 'Completed':
      return 'Completed'
    case 'Expired':
      return 'Expired'
    default:
      return status
  }
}
