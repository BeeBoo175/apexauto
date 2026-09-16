export interface ShowroomPhoto {
  src: string;
  alt: string;
}

const SHOWROOM_PHOTOS: Record<string, ShowroomPhoto> = {
  glenmarie: {
    src: "/images/showroom-panorama.jpg",
    alt: "ApexAuto Glenmarie experience gallery floor with vehicle lineup",
  },
  "petaling jaya": {
    src: "/images/showroom-seasia-floor.jpg",
    alt: "ApexAuto Petaling Jaya showroom and display bays",
  },
  "bayan lepas": {
    src: "/images/showroom-architecture.jpg",
    alt: "ApexAuto Bayan Lepas showroom floor",
  },
};

const DEFAULT_PHOTO: ShowroomPhoto = {
  src: "/images/showroom-panorama.jpg",
  alt: "ApexAuto vehicle display floor",
};

export function showroomPhoto(shortLabel: string): ShowroomPhoto {
  return SHOWROOM_PHOTOS[shortLabel.toLowerCase()] ?? DEFAULT_PHOTO;
}
