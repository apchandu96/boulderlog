﻿using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System;
using System.Collections.Generic;

namespace Boulderlog.Data.Models
{
    [Table("Gym")]
    public class Gym
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int? Id { get; set; }

        [MaxLength(255)]
        public required string Name { get; set; }

        [MaxLength(1000)]
        public required string Walls { get; set; }

        public ICollection<Grade> Grades { get; set; }
    }
}