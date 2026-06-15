import { motion } from 'framer-motion'

type FullscreenLoaderProps = {
  label: string
}

export function FullscreenLoader({ label }: FullscreenLoaderProps) {
  return (
    <div className="fullscreen-loader">
      <motion.div
        className="fullscreen-loader__ball"
        animate={{ scale: [1, 1.12, 1], rotate: [0, 10, -10, 0] }}
        transition={{ duration: 1.4, repeat: Number.POSITIVE_INFINITY, ease: 'easeInOut' }}
      >
        8
      </motion.div>
      <p>{label}</p>
    </div>
  )
}
